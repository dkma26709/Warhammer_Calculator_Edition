"""
Extract non-unit content from WAP army book PDFs.
Extracts: army special rules, magic items, lores of magic, faction-specific upgrades.

Uses column-aware extraction (same two-column PDF layout as core rules).
"""

import re
import json
import sys
import os
import glob
import pdfplumber


# ── PDF helpers ──────────────────────────────────────────────────────────────

def extract_column_text(page):
    """Extract text from a page reading left column then right column."""
    mid = page.width / 2
    left = page.crop((0, 0, mid, page.height))
    right = page.crop((mid, 0, page.width, page.height))
    lt = left.extract_text() or ''
    rt = right.extract_text() or ''
    lt = re.sub(r'\n\s*\d{1,3}\s*$', '', lt.strip())
    rt = re.sub(r'\n\s*\d{1,3}\s*$', '', rt.strip())
    return lt, rt


def reconstruct_header(left_first, right_first):
    """Reconstruct a full-page header from column-split fragments.

    When a header spans the full page width (e.g. "VAMPIRIC POWERS"),
    column cropping splits it: left="VAMPIRIC", right="C POWERS".
    The overlap (the "C") appears in both crops. We find this overlap
    and rejoin to get "VAMPIRIC POWERS".
    """
    if not left_first or not right_first:
        return left_first or right_first or ''
    # Find overlap: longest suffix of left_first that matches a prefix of right_first
    max_overlap = min(len(left_first), len(right_first))
    for overlap_len in range(max_overlap, 0, -1):
        if left_first[-overlap_len:] == right_first[:overlap_len]:
            return left_first + right_first[overlap_len:]
    # No character overlap — just join with space
    return left_first + ' ' + right_first


def is_split_header(left_first, right_first):
    """Check if left+right first lines are parts of a column-split page header.

    Page headers span the full page width and get split by column cropping.
    The last 1-5 chars of the left portion overlap with the first chars of
    the right portion (e.g. "VAMPIRIC" + "C POWERS" → overlap at "C").
    """
    if not left_first or not right_first:
        return False
    if len(right_first) >= 30:
        return False
    # Exclude points entries
    if re.search(r'\d+\s+points?', right_first, re.IGNORECASE):
        return False
    # Both must have no lowercase letters (headers are ALL-CAPS)
    if re.search(r'[a-z]', left_first) or re.search(r'[a-z]', right_first):
        return False
    # Check for character overlap at boundary (1-5 chars)
    for overlap in range(min(5, len(left_first), len(right_first)), 0, -1):
        if left_first[-overlap:] == right_first[:overlap]:
            # For 1-char overlaps, guard against false positives where two
            # independent headers coincidentally share a boundary character
            # (e.g. "REIGN OF CHAOS" + "SUMMONED FROM BEYOND" overlap on "S")
            if overlap == 1:
                left_words = left_first.split()
                right_words = right_first.split()
                last_lw = left_words[-1] if left_words else ''
                first_rw = right_words[0] if right_words else ''
                if len(last_lw) >= 5 and len(first_rw) >= 5:
                    # Both sides end/start with complete words — coincidental
                    continue
            return True
    # No character overlap — check if left ends with a preposition/article
    # suggesting the header continues in the right column
    # (e.g. "THE LORE OF" + "DARK MAGIC" splits cleanly at word boundary)
    left_words = left_first.split()
    last_left = left_words[-1] if left_words else ''
    if last_left in ('OF', 'THE', 'AND', 'OR', 'FOR', 'IN', 'AT', 'BY', 'FROM', 'TO'):
        return True
    return False


def extract_page_text(page):
    """Extract column-sequential text from a page, reconstructing split headers.

    Returns (header_or_none, body_text) where header is the reconstructed
    full-page header if one was detected, or None.
    """
    lt, rt = extract_column_text(page)
    if not lt and not rt:
        return None, ''

    lt_lines = lt.split('\n') if lt else []
    rt_lines = rt.split('\n') if rt else []
    left_first = lt_lines[0].strip() if lt_lines else ''
    right_first = rt_lines[0].strip() if rt_lines else ''

    if is_split_header(left_first, right_first):
        # Reconstruct the full header
        full_header = reconstruct_header(left_first, right_first)
        header_lines = 1

        # Check for multi-line header (2nd line also a split fragment)
        # This handles long headers like "THE LORE OF THE GREAT MAW"
        # where "MAW" wraps to a second line split as "MA" / "AW"
        left_second = lt_lines[1].strip() if len(lt_lines) > 1 else ''
        right_second = rt_lines[1].strip() if len(rt_lines) > 1 else ''
        if (left_second and right_second
                and is_split_header(left_second, right_second)
                and len(left_second) <= 5 and len(right_second) <= 5):
            full_header += ' ' + reconstruct_header(left_second, right_second)
            header_lines = 2

        left_body = '\n'.join(lt_lines[header_lines:]).strip()
        right_body = '\n'.join(rt_lines[header_lines:]).strip()
        parts = [full_header]
        if left_body:
            parts.append(left_body)
        if right_body:
            parts.append(right_body)
        return full_header, '\n'.join(parts)
    else:
        parts = []
        if lt:
            parts.append(lt)
        if rt:
            parts.append(rt)
        return None, '\n'.join(parts)


def get_column_text(page):
    """Get left+right column text joined sequentially."""
    _, text = extract_page_text(page)
    return text


def clean_text(text):
    """Fix common PDF artifacts."""
    return text.replace('\u2019', "'").replace('\u2018', "'").replace('\u2013', '-').replace('\u2014', '-')


# ── Section detection ────────────────────────────────────────────────────────

# Section headers that mark the start of different content areas
# These are checked against the combined first ~80 chars of left+right columns
SECTION_PATTERNS = {
    'army_rules': r'ARMY\s*SPEC.*?(?:RULES|CIAL)',
    'magic_weapons': r'MAGIC\s+WEAPONS?',
    'magic_armour': r'MAGIC\s+ARMOUR',
    'talismans': r'^TALISMANS?',
    'arcane_items': r'ARCANE\s+ITEMS?',
    'enchanted_items': r'ENCHANTED\s+ITEMS?',
    'magic_standards': r'MAGIC\s+STANDARDS?',
    'lords': r'^LOR\b.*\bRDS\b',  # Matches "LORDS" split across columns
    'heroes': r'^HERO.*OES',  # Matches "HEROES" split across columns
    'core': r'CORE\s+U(?:NITS?)?',
    'special_units': r'SPECIAL\s+(?:U(?:NITS?)?|L\s+UNITS)',
    'rare': r'RARE\s+U(?:NITS?)?',
    'special_characters': r'SPECIAL\s+CH(?:ARACTERS?)?',
}

# Magic item category headers as they appear in the PDF
MAGIC_ITEM_CATEGORIES = [
    ('MAGIC WEAPONS', 'magic_weapon'),
    ('MAGIC ARMOUR', 'magic_armour'),
    ('TALISMANS', 'talisman'),
    ('ARCANE ITEMS', 'arcane_item'),
    ('ENCHANTED ITEMS', 'enchanted_item'),
    ('MAGIC STANDARDS', 'magic_standard'),
    # Dwarf-specific
    ('WEAPON RUNES', 'weapon_rune'),
    ('ARMOUR RUNES', 'armour_rune'),
    ('TALISMANIC RUNES', 'talismanic_rune'),
    ('BANNER RUNES', 'banner_rune'),
    ('ENGINEERING RUNES', 'engineering_rune'),
    ('RUNIC ITEMS', 'runic_item'),
]


def find_sections(pdf):
    """Scan the PDF and find page ranges for each section."""
    sections = []
    for pn, page in enumerate(pdf.pages):
        lt, rt = extract_column_text(page)

        # Skip table of contents (page 2 usually)
        if pn <= 1 and ('CONT' in lt[:10] or '..........' in lt):
            continue

        # Combine first lines of each column for matching
        lt_first = lt.split('\n')[0].strip() if lt else ''
        rt_first = rt.split('\n')[0].strip() if rt else ''
        combined_first = lt_first + ' ' + rt_first

        # Check for unit pages first (they have Profile headers)
        has_profile = 'Profile' in lt[:200] and ('M W' in lt[:200] or 'WS BS' in lt[:200])

        matched = False
        for section_name, pattern in SECTION_PATTERNS.items():
            if re.search(pattern, combined_first):
                # Extra validation for ambiguous patterns
                if section_name == 'lords' and not has_profile:
                    continue  # Skip if it's a Lore page, not Lords
                if section_name == 'heroes' and not has_profile:
                    continue
                if section_name == 'army_rules' and pn <= 1:
                    continue  # Skip TOC

                sections.append((pn, section_name))
                matched = True
                break

        if not matched and has_profile:
            # Detect unit section from profile content
            if 'SPECIAL CH' in lt_first or 'SPECIAL CH' in rt_first:
                sections.append((pn, 'special_characters'))
            elif 'LOR' in lt_first and 'RDS' in rt_first:
                sections.append((pn, 'lords'))
            elif 'HERO' in lt_first and 'OES' in rt_first:
                sections.append((pn, 'heroes'))

    return sections


def get_section_pages(sections, section_name):
    """Get the page range for a section (0-indexed)."""
    pages = [pn for pn, name in sections if name == section_name]
    return pages


def get_non_unit_pages(pdf, sections):
    """Get the page number of the first unit page (Lords/Heroes/Core)."""
    unit_sections = {'lords', 'heroes', 'core', 'special_units', 'rare', 'special_characters'}
    first_unit_page = None
    for pn, name in sections:
        if name in unit_sections:
            first_unit_page = pn
            break
    return first_unit_page or len(pdf.pages)


def get_non_unit_text(pdf, sections):
    """Get all column-sequential text from non-unit pages, skipping page 1-2 (cover/TOC).
    Reconstructs full headers from column-split fragments."""
    first_unit_page = get_non_unit_pages(pdf, sections)
    texts = []
    for pn in range(2, first_unit_page):  # Skip cover and TOC
        _, page_text = extract_page_text(pdf.pages[pn])
        if page_text:
            texts.append(page_text)

    return '\n'.join(texts)


# ── Army Special Rules ───────────────────────────────────────────────────────

def extract_army_rules(text):
    """Extract army special rules (name + description) from combined non-unit text."""
    rules = []

    # Find the ARMY SPECIAL RULES section
    m = re.search(r'ARMY\s+SPECIAL\s+RULES\s*\n', text)
    if not m:
        return rules

    # Find where army rules end (next major section)
    # With reconstructed headers, full section names are available
    section_end_pattern = re.compile(
        r'\n(?:(?:THE\s+)?LORE\s+OF|BLESSED\s+SPAWNINGS?|DISCIPLINES?\s+OF|'
        r'MAGIC\s+(?:WEAPONS|ARMOUR|ITEMS)|TALISMANS?|ENCHANTED\s+ITEMS?|'
        r'ARCANE\s+ITEMS?|MAGIC\s+STANDARDS?|'
        r'KNIGHTLY\s+ORDERS?|RUNIC\s+ITEMS?|WEAPON\s+RUNES?|'
        r'GIFTS?\s+OF\s+(?:CHAOS|KHORNE|NURGLE|SLAANESH|TZEENTCH)|DAEMONIC\s+GIFTS?|'
        r'VAMPIRIC\s+POWERS?|BIG\s+NAMES?|WAAAGH!\s+MAGIC|'
        r'MUTATIONS?\s+&\s+POWERS?|SKAVEN\s+WARGEAR|CLAN\s+SKRYRE)',
        re.MULTILINE
    )
    end_m = section_end_pattern.search(text, m.end())
    end_pos = end_m.start() if end_m else len(text)

    rules_text = text[m.end():end_pos]

    # Parse rules: ALL-CAPS heading followed by description
    lines = rules_text.split('\n')
    heading_pattern = re.compile(r'^([A-Z][A-Z\s\'\-\(\)]+)$')

    # Skip intro paragraph
    i = 0
    while i < len(lines):
        stripped = lines[i].strip()
        if heading_pattern.match(stripped) and len(stripped) > 3:
            break
        i += 1

    headings = []
    for j in range(i, len(lines)):
        stripped = lines[j].strip()
        if heading_pattern.match(stripped) and len(stripped) > 3:
            headings.append((j, stripped))

    for idx, (line_num, name) in enumerate(headings):
        start = line_num + 1
        end = headings[idx + 1][0] if idx + 1 < len(headings) else len(lines)
        description = '\n'.join(lines[start:end]).strip()
        if description:
            rules.append({
                "name": name.title() if name.isupper() else name,
                "description": description,
            })

    # Merge split sub-headers: entries whose name ends with a 1-2 char fragment
    # are likely column-split sub-headers (e.g. "EYE OF THE G" + "GODS TABLE")
    SHORT_WORDS = {'Of', 'A', 'As', 'At', 'By', 'In', 'Is', 'On', 'Or', 'To', 'Vs'}
    merged = []
    i = 0
    while i < len(rules):
        rule = rules[i]
        words = rule['name'].split()
        last_word = words[-1] if words else ''
        if len(last_word) <= 2 and last_word not in SHORT_WORDS and i + 1 < len(rules):
            next_rule = rules[i + 1]
            merged_name = reconstruct_header(
                rule['name'].upper(), next_rule['name'].upper()
            )
            merged.append({
                'name': merged_name.title(),
                'description': rule['description'] + '\n' + next_rule['description'],
            })
            i += 2
        else:
            merged.append(rule)
            i += 1

    return merged


# ── Magic Items ──────────────────────────────────────────────────────────────

def extract_magic_items(text):
    """Extract all magic items organized by category from combined non-unit text."""
    items = []

    current_category = None
    lines = text.split('\n')

    # Item header pattern: "NAME <N> points" or "NAME <N>/<N>/<N> points"
    item_header_re = re.compile(r'^([A-Z][A-Z\s\'\-\,\.]+?)\s+(\d+(?:/\d+)*)\s+points?\s*$')

    i = 0
    while i < len(lines):
        stripped = lines[i].strip()

        # Check for category headers
        for cat_header, cat_key in MAGIC_ITEM_CATEGORIES:
            if stripped == cat_header or stripped.startswith(cat_header):
                current_category = cat_key
                break

        if current_category is None:
            i += 1
            continue

        # Check for item header
        item_match = item_header_re.match(stripped)
        if item_match:
            name = item_match.group(1).strip()
            points_str = item_match.group(2)

            # Check if previous line is a name continuation
            # (ALL-CAPS line that doesn't end with points and isn't a category header)
            if i > 0:
                prev = lines[i - 1].strip()
                if (prev and prev.isupper() and len(prev) > 3
                        and not item_header_re.match(prev)
                        and prev not in [ch for ch, _ in MAGIC_ITEM_CATEGORIES]):
                    name = prev + ' ' + name

            # Collect description
            desc_lines = []
            for j in range(i + 1, len(lines)):
                next_line = lines[j].strip()
                if not next_line:
                    continue
                # Stop at next item header (check both single and multi-line)
                if item_header_re.match(next_line):
                    break
                # Check if this ALL-CAPS line + next line form an item header
                if (next_line.isupper() and len(next_line) > 3
                        and j + 1 < len(lines) and item_header_re.match(lines[j + 1].strip())):
                    break
                # Stop at category headers
                is_category = False
                for cat_header, _ in MAGIC_ITEM_CATEGORIES:
                    if next_line == cat_header or next_line.startswith(cat_header):
                        is_category = True
                        break
                if is_category:
                    break
                desc_lines.append(next_line)

            description = '\n'.join(desc_lines).strip()

            item = {
                "name": name.title() if name.isupper() else name,
                "points": points_str,
                "category": current_category,
                "description": description,
            }

            # Check for restrictions in first line of description
            restriction_match = re.match(
                r'^(.*?only\.)\s*(.*)',
                description,
                re.DOTALL
            )
            if restriction_match:
                item['restriction'] = restriction_match.group(1).strip()

            items.append(item)

        i += 1

    return items


# ── Lores of Magic ───────────────────────────────────────────────────────────

def extract_lores(pdf, first_unit_page):
    """Extract faction-specific lores of magic."""
    lores = []

    # Find lore pages by looking for actual spell definitions (Level N Cast on N+)
    lore_pages = []
    for pn in range(2, first_unit_page):  # Skip cover/TOC
        text = get_column_text(pdf.pages[pn])
        has_spell_def = bool(re.search(r'(?:Level \d+|Signature Spell)\s+Cast on \d+\+', text))
        if has_spell_def:
            lore_pages.append(pn)

    if not lore_pages:
        return lores

    # Group consecutive pages into lore blocks, splitting when a new lore starts.
    # A page starts a new lore if its reconstructed header contains "LORE OF".
    lore_blocks = []
    current_block = [lore_pages[0]]
    for pn in lore_pages[1:]:
        gap = pn - current_block[-1]
        if gap > 2:
            # Non-consecutive — definitely a new block
            lore_blocks.append(current_block)
            current_block = [pn]
        else:
            # Check if this page starts a new lore (has a "LORE OF" header)
            header, _ = extract_page_text(pdf.pages[pn])
            if header and re.search(r'LORE\s+OF', header):
                # New lore starts here
                lore_blocks.append(current_block)
                current_block = [pn]
            else:
                current_block.append(pn)
    lore_blocks.append(current_block)

    for block in lore_blocks:
        # Collect text using header reconstruction
        text_parts = []
        for pn in block:
            _, page_text = extract_page_text(pdf.pages[pn])
            if page_text:
                text_parts.append(page_text)

        text = clean_text('\n'.join(text_parts))

        lore = parse_lore(text, name_hint=None)
        if lore:
            lores.append(lore)

    return lores


def parse_lore(text, name_hint=None):
    """Parse a lore of magic from text."""
    lore = {"name": name_hint, "attribute": None, "spells": []}

    lines = text.split('\n')

    # Find lore name - usually "THE LORE OF <NAME>" or "LORE OF <NAME>"
    if not lore['name']:
        for line in lines:
            stripped = line.strip()
            m = re.match(r'(?:THE\s+)?LORE\s+OF\s+(.+)', stripped, re.IGNORECASE)
            if m:
                name = m.group(1).strip()
                name = re.sub(r'^[A-Z]\s+', '', name)
                lore['name'] = name.title() if name.isupper() else name
                break

    # Try extracting from lore attribute text as fallback
    if not lore['name']:
        attr_m = re.search(r'Lore of\s+([A-Z][a-z]+(?:\s+[A-Z][a-z]+)*)', text)
        if attr_m:
            lore['name'] = attr_m.group(1).strip()

    # Find Lore Attribute
    attr_match = re.search(
        r'Lore Attribute\s*\n(.*?)(?=Signature Spell|Level \d)',
        text,
        re.DOTALL
    )
    if attr_match:
        lore['attribute'] = attr_match.group(1).strip()

    # Find spells
    # Pattern: SPELL_NAME\n(Signature Spell|Level N) Cast on N+\n description
    spell_pattern = re.compile(
        r'^([A-Z][A-Z\s\'\-]+)\n'
        r'(Signature Spell|Level\s+\d+)\s+Cast\s+on\s+(\d+)\+\s*\n'
        r'(.*?)(?=\n[A-Z][A-Z\s\'\-]+\n(?:Signature Spell|Level \d)|$)',
        re.MULTILINE | re.DOTALL
    )

    for m in spell_pattern.finditer(text):
        name = m.group(1).strip()
        level_str = m.group(2).strip()
        cast_value = int(m.group(3))
        description = m.group(4).strip()

        # Skip section headers
        if name in ('THE LORE OF', 'LORE OF'):
            continue

        level = None
        if level_str == 'Signature Spell':
            level = 0
        else:
            level_m = re.match(r'Level\s+(\d+)', level_str)
            if level_m:
                level = int(level_m.group(1))

        # Extract spell type from description
        spell_type = None
        type_match = re.search(
            r'is\s+(?:a\s+|an\s+)?(augment|hex|direct damage|magic missile|'
            r'conveyance|summoning|special type of|special)\s+(?:spell|type)',
            description,
            re.IGNORECASE
        )
        if type_match:
            spell_type = type_match.group(1).strip().lower()
            if spell_type == 'special type of':
                spell_type = 'special'
        # Also check for "Remains in play" prefix + type
        if not spell_type:
            type_match = re.search(
                r'(?:Remains in play\.?\s+).+?is\s+(?:a\s+|an\s+)?(augment|hex|direct damage|'
                r'magic missile|conveyance|summoning|special)\s+(?:spell|type)',
                description,
                re.IGNORECASE
            )
            if type_match:
                spell_type = type_match.group(1).strip().lower()

        spell = {
            "name": name.title() if name.isupper() else name,
            "level": level,
            "castValue": cast_value,
            "description": description,
        }
        if spell_type:
            spell['type'] = spell_type

        lore['spells'].append(spell)

    if not lore['name'] and not lore['spells']:
        return None

    return lore


# ── Faction-specific Upgrades ────────────────────────────────────────────────

def extract_faction_upgrades(text):
    """Extract faction-specific upgrades (Blessed Spawnings, Disciplines, Knightly Orders, etc.)."""
    upgrades = {}

    # Detect upgrade section types and their boundaries
    # With reconstructed headers, full section names are available
    upgrade_sections = [
        (r'BLESSED\s+SPAWNINGS?', 'blessed_spawnings'),
        (r'DISCIPLINES?\s+OF', 'disciplines'),
        (r'KNIGHTLY\s+ORDERS?', 'knightly_orders'),
        (r'GIFTS?\s+OF\s+CHAOS', 'gifts_of_chaos'),
        (r'GIFTS?\s+OF\s+KHAINE', 'gifts_of_khaine'),
        (r'DAEMONIC\s+GIFTS?', 'daemonic_gifts'),
        (r'VAMPIRIC\s+POWERS?', 'vampiric_powers'),
        (r'BIG\s+NAMES?', 'big_names'),
        (r'MUTATIONS?\s+&\s+POWERS?', 'mutations_and_powers'),
    ]

    # Find all upgrade sections with their positions
    found_sections = []
    for pattern, section_key in upgrade_sections:
        match = re.search(pattern, text)
        if match:
            found_sections.append((match.start(), match.end(), section_key))

    # Sort by position
    found_sections.sort()

    # Also find major section boundaries that end upgrade sections
    end_markers = re.compile(
        r'\n(?:MAGIC\s+(?:WEAPONS|ARMOUR|C\s*ITEMS)|TALISMANS?|ENCHANTED|ARCANE|'
        r'MAGIC\s+STANDARDS?|WEAPON\s+RUNES|ARMOUR\s+RUNES|LOR\b|HERO|CORE\s+U)'
    )

    for i, (start, header_end, section_key) in enumerate(found_sections):
        # End at next upgrade section or next major section
        end = len(text)
        if i + 1 < len(found_sections):
            end = found_sections[i + 1][0]

        # Also check for end markers
        m = end_markers.search(text, header_end + 1)
        if m and m.start() < end:
            end = m.start()

        section_text = text[start:end]
        items = parse_upgrade_items(section_text)
        if items:
            upgrades[section_key] = items

    return upgrades


def parse_upgrade_items(text):
    """Parse upgrade items (name + points + description) from a section."""
    items = []
    lines = text.split('\n')

    # Standard pattern: "ITEM_NAME <N> points"
    item_header_re = re.compile(r'^([A-Z][A-Z\s\'\-\,\.]+?)\s+(\d+(?:/\d+)*)\s+points?\s*$')

    # Knightly Orders pattern: "ORDER_NAME\nCharacters <N> points\nCavalry <N> points..."
    order_header_re = re.compile(r'^([A-Z][A-Z\s\'\-]+)$')
    # The cost must be a real one ("free" or a number). Without that, a section header
    # followed by its "Characters may take..." intro sentence parses as an order entry.
    order_cost_re = re.compile(
        r'^(Characters?|Cavalry|Infantry|Monstrous\s+\w+)\s+(free\b|\d+\b.*)$')

    for i, line in enumerate(lines):
        stripped = line.strip()

        # Standard item match
        item_match = item_header_re.match(stripped)
        if item_match:
            name = item_match.group(1).strip()
            points_str = item_match.group(2)

            # Check if previous line is a name continuation
            if i > 0:
                prev = lines[i - 1].strip()
                if (prev and prev.isupper() and len(prev) > 3
                        and not item_header_re.match(prev)):
                    name = prev + ' ' + name

            # Collect description
            desc_lines = []
            for j in range(i + 1, len(lines)):
                next_line = lines[j].strip()
                if not next_line:
                    continue
                if item_header_re.match(next_line):
                    break
                # Check for multi-line name of next item
                if (next_line.isupper() and len(next_line) > 3
                        and j + 1 < len(lines) and item_header_re.match(lines[j + 1].strip())):
                    break
                desc_lines.append(next_line)

            description = '\n'.join(desc_lines).strip()

            item = {
                "name": name.title() if name.isupper() else name,
                "points": points_str,
                "description": description,
            }

            # Check for restrictions
            restriction_match = re.match(r'^(.*?only\.)\s*(.*)', description, re.DOTALL)
            if restriction_match:
                item['restriction'] = restriction_match.group(1).strip()

            items.append(item)
            continue

        # Knightly Order match: ALL-CAPS name followed by cost lines
        if order_header_re.match(stripped) and i + 1 < len(lines):
            next_stripped = lines[i + 1].strip() if i + 1 < len(lines) else ''
            # Check if next line is a cost line (Characters/Cavalry/Infantry...)
            if order_cost_re.match(next_stripped):
                name = stripped

                # Collect cost lines and description
                cost_lines = []
                desc_lines = []
                in_costs = True
                for j in range(i + 1, len(lines)):
                    next_line = lines[j].strip()
                    if not next_line:
                        continue
                    if in_costs and order_cost_re.match(next_line):
                        cost_lines.append(next_line)
                    else:
                        in_costs = False
                        # Stop at next order/item header
                        if order_header_re.match(next_line) and j + 1 < len(lines):
                            peek = lines[j + 1].strip() if j + 1 < len(lines) else ''
                            if order_cost_re.match(peek) or item_header_re.match(peek):
                                break
                        if item_header_re.match(next_line):
                            break
                        desc_lines.append(next_line)

                costs = '\n'.join(cost_lines)
                description = '\n'.join(desc_lines).strip()

                item = {
                    "name": name.title() if name.isupper() else name,
                    "points": costs,
                    "description": description,
                }
                items.append(item)

    return items


# ── Main extraction ──────────────────────────────────────────────────────────

# Map PDF filenames to faction names
FACTION_MAP = {
    'Beastmen': 'beastmen',
    'Bretonnia': 'bretonnia',
    'Daemons of Chaos': 'daemons-of-chaos',
    'Dark Elves': 'dark-elves',
    'Dwarfs': 'dwarfs',
    'High Elves': 'high-elves',
    'Lizardmen': 'lizardmen',
    'Ogre Kingdoms': 'ogre-kingdoms',
    'Orcs & Goblins': 'orcs-and-goblins',
    'Skaven': 'skaven',
    'The Empire': 'the-empire',
    'Tomb Kings': 'tomb-kings',
    'Vampire Counts': 'vampire-counts',
    'Warriors of Chaos': 'warriors-of-chaos',
    'Wood Elves': 'wood-elves',
}


def extract_army_extras(pdf_path, faction_name=None):
    """Extract all non-unit content from an army book PDF."""
    with pdfplumber.open(pdf_path) as pdf:
        sections = find_sections(pdf)
        first_unit_page = get_non_unit_pages(pdf, sections)

        # Get combined text from all non-unit pages
        non_unit_text = clean_text(get_non_unit_text(pdf, sections))

        result = {
            "factionName": faction_name or os.path.basename(pdf_path),
            "armySpecialRules": extract_army_rules(non_unit_text),
            "magicItems": extract_magic_items(non_unit_text),
            "loresOfMagic": extract_lores(pdf, first_unit_page),
            "factionUpgrades": extract_faction_upgrades(non_unit_text),
        }

    return result


def find_latest_pdfs(pdf_dir):
    """Find the latest version of each faction PDF."""
    pdfs = {}
    for pdf_file in glob.glob(os.path.join(pdf_dir, 'Warhammer - *.pdf')):
        basename = os.path.basename(pdf_file)
        # Skip core rules
        if 'Game of Fantasy' in basename:
            continue

        # Extract faction name and version: "Warhammer - Lizardmen 1.64.pdf"
        m = re.match(r'Warhammer - (.+?)\s+([\d.]+)\.pdf$', basename)
        if m:
            faction = m.group(1)
            version = m.group(2)
            if faction not in pdfs or version > pdfs[faction][1]:
                pdfs[faction] = (pdf_file, version)

    return {k: v[0] for k, v in pdfs.items()}


def main():
    import argparse
    parser = argparse.ArgumentParser(description='Extract army book non-unit content from WAP PDFs')
    parser.add_argument('pdf', nargs='?', help='Path to a single army book PDF')
    parser.add_argument('--faction', default=None, help='Faction name')
    parser.add_argument('--all', action='store_true', help='Process all PDFs in the directory')
    parser.add_argument('--dir', default=None, help='Directory containing PDFs (for --all)')
    parser.add_argument('--output', default=None, help='Output JSON file or directory')
    parser.add_argument('--pretty', action='store_true', help='Pretty-print JSON')
    args = parser.parse_args()

    if args.all:
        pdf_dir = args.dir or os.path.dirname(os.path.abspath(__file__)) + '/..'
        output_dir = args.output or os.path.join(os.path.dirname(os.path.abspath(__file__)), 'json')
        os.makedirs(output_dir, exist_ok=True)

        pdfs = find_latest_pdfs(pdf_dir)
        total_items = 0
        total_rules = 0
        total_spells = 0

        for faction_display, pdf_path in sorted(pdfs.items()):
            faction_key = FACTION_MAP.get(faction_display, faction_display.lower().replace(' ', '-'))
            result = extract_army_extras(pdf_path, faction_key)

            output_file = os.path.join(output_dir, f'{faction_key}-extras.json')
            json_str = json.dumps(result, indent=2 if args.pretty else None, ensure_ascii=False)
            with open(output_file, 'w', encoding='utf-8') as f:
                f.write(json_str)

            n_rules = len(result['armySpecialRules'])
            n_items = len(result['magicItems'])
            n_lores = len(result['loresOfMagic'])
            n_spells = sum(len(l['spells']) for l in result['loresOfMagic'])
            n_upgrades = sum(len(v) for v in result['factionUpgrades'].values())

            total_items += n_items
            total_rules += n_rules
            total_spells += n_spells

            print(f'{faction_key}: {n_rules} rules, {n_items} items, '
                  f'{n_lores} lores ({n_spells} spells), {n_upgrades} upgrades -> {output_file}')

        print(f'\nTotal: {total_rules} army rules, {total_items} magic items, {total_spells} spells')

    elif args.pdf:
        result = extract_army_extras(args.pdf, args.faction)

        json_str = json.dumps(result, indent=2 if args.pretty else None, ensure_ascii=False)

        if args.output:
            with open(args.output, 'w', encoding='utf-8') as f:
                f.write(json_str)
            print(f"Extracted -> {args.output}")
        else:
            print(json_str)

        print(f"\nSummary:")
        print(f"  Army rules: {len(result['armySpecialRules'])}")
        for r in result['armySpecialRules']:
            print(f"    - {r['name']}")
        print(f"  Magic items: {len(result['magicItems'])}")
        for cat in set(i['category'] for i in result['magicItems']):
            count = sum(1 for i in result['magicItems'] if i['category'] == cat)
            print(f"    {cat}: {count}")
        print(f"  Lores of magic: {len(result['loresOfMagic'])}")
        for lore in result['loresOfMagic']:
            print(f"    {lore['name']}: {len(lore['spells'])} spells")
        print(f"  Faction upgrades: {sum(len(v) for v in result['factionUpgrades'].values())}")
        for k, v in result['factionUpgrades'].items():
            print(f"    {k}: {len(v)}")
    else:
        parser.print_help()


if __name__ == '__main__':
    main()
