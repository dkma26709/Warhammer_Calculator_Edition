"""
Extract core rules data from the WAP 9th Edition PDF.
Extracts combat tables, weapon/armour profiles, and common special rules.

The PDF uses a two-column layout. Tables span full width but text content
is split into left/right columns. We use pdfplumber's extract_tables() for
tables and page.crop() for column-aware text extraction.
"""

import re
import json
import sys
import pdfplumber


def extract_column_text(page):
    """Extract text from a page reading left column then right column."""
    mid = page.width / 2
    left = page.crop((0, 0, mid, page.height))
    right = page.crop((mid, 0, page.width, page.height))
    lt = left.extract_text() or ''
    rt = right.extract_text() or ''
    # Strip page numbers from end of each column
    lt = re.sub(r'\n\s*\d{1,3}\s*$', '', lt.strip())
    rt = re.sub(r'\n\s*\d{1,3}\s*$', '', rt.strip())
    return lt, rt


def find_tables_on_page(page):
    """Extract structured tables from a page using pdfplumber's table detection."""
    return page.extract_tables() or []


def extract_tables_from_pdf(pdf_path):
    """Extract the combat tables (to-hit, to-wound, armour save mods) using table detection.

    The to-hit and to-wound charts are both 10x10 grids with an identical ['', '1'..'10']
    header, so they are told apart by the page's column-axis caption — "TARGET'S WEAPON
    SKILL" against "TARGET'S TOUGHNESS" — rather than by which one the page scan reaches
    first. Order alone silently mislabels them if a book lays the chapter out differently.
    """
    to_hit = None
    to_wound = None
    armour_mods = None
    unlabelled = []

    with pdfplumber.open(pdf_path) as pdf:
        for page in pdf.pages:
            page_text = (page.extract_text() or '').upper()
            names_ws = 'WEAPON SKILL' in page_text
            names_t = 'TOUGHNESS' in page_text

            tables = find_tables_on_page(page)
            for table in tables:
                if not table or len(table) < 2:
                    continue

                header = table[0]
                # To-Hit / To-Wound: header row is ['', '1', '2', ..., '10']
                if (header and len(header) == 11 and header[0] in ('', None)
                        and str(header[1]) == '1' and str(header[-1]) == '10'):
                    parsed = parse_combat_table(table)
                    if parsed:
                        if names_ws and not names_t:
                            to_hit = to_hit or parsed
                        elif names_t and not names_ws:
                            to_wound = to_wound or parsed
                        else:
                            unlabelled.append(parsed)

                # Armour save modifiers: header ['Strength:', 'Save modifier:']
                if header and len(header) == 2:
                    h0 = str(header[0] or '').strip().lower()
                    h1 = str(header[1] or '').strip().lower()
                    if 'strength' in h0 and 'modifier' in h1:
                        armour_mods = parse_armour_mod_table(table)

    # Fall back to document order only for charts whose page gave no usable caption.
    for parsed in unlabelled:
        if to_hit is None:
            to_hit = parsed
            print('  WARNING: a 10x10 chart had no axis caption; assuming To-Hit by position')
        elif to_wound is None:
            to_wound = parsed
            print('  WARNING: a 10x10 chart had no axis caption; assuming To-Wound by position')

    return to_hit, to_wound, armour_mods


def parse_combat_table(table):
    """Parse a 10x10 combat table from pdfplumber table data."""
    rows = []
    for row in table[1:]:  # Skip header
        if not row or len(row) < 11:
            continue
        values = []
        for cell in row[1:]:  # Skip row label
            cell_str = str(cell or '').strip()
            m = re.match(r'(\d)\+', cell_str)
            if m:
                values.append(int(m.group(1)))
            else:
                break
        if len(values) == 10:
            rows.append(values)
    return rows if len(rows) == 10 else None


# Hyphen-minus, en dash, em dash, figure dash and the Unicode minus sign all show up as
# "no modifier" or as the sign on a negative one, depending on the book's typesetting.
DASHES = '-‐‑‒–—−'


def parse_armour_mod_table(table):
    """Parse armour save modifier table."""
    modifiers = {}
    for row in table[1:]:  # Skip header
        if not row or len(row) < 2:
            continue
        s_val = str(row[0] or '').strip()
        mod_str = str(row[1] or '').strip()
        if not s_val.isdigit():
            continue
        if mod_str in DASHES:
            modifiers[f"S{s_val}"] = 0
        else:
            m = re.match(rf'[{DASHES}]\s*(\d+)', mod_str)
            if m:
                modifiers[f"S{s_val}"] = -int(m.group(1))
    return modifiers


def extract_column_text_range(pdf_path, start_page, end_page):
    """Extract column-sequential text from a range of pages (1-indexed)."""
    texts = []
    with pdfplumber.open(pdf_path) as pdf:
        for pn in range(start_page - 1, min(end_page, len(pdf.pages))):
            lt, rt = extract_column_text(pdf.pages[pn])
            if lt:
                texts.append(lt)
            if rt:
                texts.append(rt)
    return '\n'.join(texts)


def find_page_ranges(pdf_path):
    """Scan the PDF to find page ranges for each chapter section."""
    ranges = {
        'weapons_start': None, 'weapons_end': None,
        'special_rules_start': None, 'special_rules_end': None,
        'shooting_start': None,
    }

    with pdfplumber.open(pdf_path) as pdf:
        for pn, page in enumerate(pdf.pages):
            lt, rt = extract_column_text(page)
            combined = (lt + '\n' + rt)

            # Weapons & Armour chapter
            if 'WEAPONS &' in lt or 'CLOSE COMBAT' in combined:
                if ranges['weapons_start'] is None:
                    # Check it's the actual weapons chapter, not a mention
                    if 'WEAPON PROFILES' in combined or 'HAND WEAPON' in combined:
                        ranges['weapons_start'] = pn + 1

            if ranges['weapons_start'] and ranges['weapons_end'] is None:
                if 'ARTILLERY' in lt[:30] or 'ARTILLERY' in rt[:30]:
                    ranges['weapons_end'] = pn + 1

            # Special Rules chapter — header is split across columns: left="SPECIAL", right="L RULES"
            if ranges['special_rules_start'] is None:
                if ('Unless stated otherwise' in combined and
                        ('SPECIAL' in lt[:30] or 'SPECIAL RULES' in combined[:60])):
                    ranges['special_rules_start'] = pn + 1

            if ranges['special_rules_start'] and ranges['special_rules_end'] is None:
                if 'DEPLOYMENT SPECIAL' in combined or 'DEPLOYMENT\nSPECIAL' in combined:
                    ranges['special_rules_end'] = pn + 1

            # Shooting section
            if 'SHOOTING PHASE' in combined and ranges['shooting_start'] is None:
                ranges['shooting_start'] = pn + 1

    return ranges


def extract_weapons_and_armour(pdf_path, start_page, end_page):
    """Extract weapon and armour profiles from the weapons chapter."""
    text = extract_column_text_range(pdf_path, start_page, end_page)
    # Fix PDF artifacts
    text = text.replace('\u2019', "'").replace('\u2018', "'").replace('\u2013', '-')

    close_combat = {}
    missile = {}
    armour = {}

    lines = text.split('\n')

    # Find all profile header lines (weapon or armour)
    # Weapon: "Range: Strength: Special Rules:"
    # Armour: "Combat: Missile: Special Rules:"
    profile_headers = []
    for i, line in enumerate(lines):
        stripped = line.strip()
        if re.match(r'^Range:\s+Strength:\s+Special Rules?:', stripped):
            profile_headers.append((i, 'weapon'))
        elif re.match(r'^Combat:\s+Missile:\s+Special Rules?:', stripped):
            profile_headers.append((i, 'armour'))

    for idx, (header_line, profile_type) in enumerate(profile_headers):
        # Weapon name: scan backwards from header for ALL-CAPS name
        name = None
        for j in range(header_line - 1, max(header_line - 5, -1), -1):
            candidate = lines[j].strip()
            if candidate and re.match(r'^[A-Z][A-Z\s/\'\-]+$', candidate):
                # Skip section headers
                if candidate in ('CLOSE COMBAT', 'WEAPONS', 'CLOSE COMBAT WEAPONS',
                                 'MISSILE WEAPONS', 'WEAPON PROFILES', 'ARMOUR'):
                    continue
                name = candidate
                break

        if not name:
            continue

        # Body: everything from header+1 to next profile header (or next ALL-CAPS weapon name + header)
        body_start = header_line + 1
        if idx + 1 < len(profile_headers):
            # End before the name line of the next weapon/armour
            next_header = profile_headers[idx + 1][0]
            # Scan backwards from next header to find its name line
            body_end = next_header
            for j in range(next_header - 1, max(next_header - 5, body_start), -1):
                candidate = lines[j].strip()
                if candidate and re.match(r'^[A-Z][A-Z\s/\'\-]+$', candidate):
                    body_end = j
                    break
        else:
            body_end = len(lines)

        body = '\n'.join(lines[body_start:body_end]).strip()

        if profile_type == 'weapon':
            weapon = parse_weapon_profile(name, body)
            if weapon:
                key = name_to_key(name)
                if weapon.get('range') == 'Combat':
                    close_combat[key] = weapon
                else:
                    missile[key] = weapon
        else:
            item = parse_armour_profile(name, body)
            if item:
                armour[name_to_key(name)] = item

    return {"closeCombat": close_combat, "missile": missile}, armour


def parse_weapon_profile(name, body):
    """Parse a weapon profile body text into a structured dict."""
    lines = body.split('\n')
    if not lines:
        return None

    # Separate profile+special rules lines from notes
    # Profile starts with "Combat" or a range number
    # Special rules continuation lines are capitalized rule names or end with commas
    # Notes are lowercase sentences, asterisk footnotes, etc.
    profile_lines = []
    notes_lines = []
    in_notes = False

    for line in lines:
        stripped = line.strip()
        if not stripped:
            continue

        if in_notes:
            notes_lines.append(stripped)
            continue

        if not profile_lines:
            # First line: the profile line itself
            profile_lines.append(stripped)
            # If profile line doesn't end with comma and the special rules part is "-", no continuation
            if stripped.endswith('-') and not stripped.endswith('(-'):
                in_notes = True
        else:
            # Check if this is a continuation of special rules or start of notes
            # Continuations: start with capital and are known rule names, or previous line ended with comma
            prev_ends_comma = profile_lines[-1].rstrip().endswith(',')

            is_rule_continuation = (
                prev_ends_comma or
                re.match(r'^(Mighty|Fight|Multiple|Armour|Quick|Volley|Rapid|Poisoned|Parry|'
                         r'Requires|Ponderous|Slow|Flaming|Move|Sniper|Ignores|Killing|'
                         r'Lightning|Ice|Stomp|Impact|Random|Natural|Regeneration|'
                         r'Ward|Dodge|Strider|Swiftstride)', stripped)
            )

            if is_rule_continuation:
                profile_lines.append(stripped)
            else:
                in_notes = True
                notes_lines.append(stripped)

    if not profile_lines:
        return None

    # Parse the profile: first line has "Range Strength SpecialRules..."
    # Combine all profile lines
    profile_text = profile_lines[0]
    weapon = {"name": name}

    # Range
    range_m = re.match(r'(Combat|(\d+)["\u201d]?)\s+', profile_text, re.IGNORECASE)
    if not range_m:
        return None

    if range_m.group(1).lower() == 'combat':
        weapon['range'] = 'Combat'
    else:
        weapon['range'] = range_m.group(2) + '"'
    rest = profile_text[range_m.end():]

    # Strength
    if rest.lower().startswith('as user'):
        weapon['strength'] = 'As user'
        rest = rest[7:].strip()
    else:
        s_match = re.match(r'([+\-]?\d+)\s*', rest)
        if s_match:
            weapon['strength'] = s_match.group(1)
            rest = rest[s_match.end():]
        else:
            return None

    # Special rules: rest of first profile line + continuation lines
    special_parts = []
    if rest.strip():
        special_parts.append(rest.strip())
    for pl in profile_lines[1:]:
        special_parts.append(pl.strip())

    special_rules = ', '.join(
        p.rstrip(',').strip() for p in special_parts if p.strip()
    )
    if special_rules == '-':
        special_rules = ''
    weapon['specialRules'] = special_rules if special_rules else None

    notes = '\n'.join(notes_lines).strip()
    if notes:
        weapon['notes'] = notes

    return weapon


def parse_armour_profile(name, body):
    """Parse an armour profile body text."""
    lines = body.split('\n')
    if not lines:
        return None

    profile_line = lines[0].strip()
    # Collect notes, stopping at chapter boundaries
    notes_lines = []
    for l in lines[1:]:
        stripped = l.strip()
        # Stop at chapter/section boundaries
        if stripped in ('ARTILLERY', 'CLOSE COMBAT', 'MISSILE WEAPONS'):
            break
        if stripped:
            notes_lines.append(stripped)

    item = {"name": name}

    # Profile format: "+1/6+ +1/6+ -" or "+3/4+ +3/4+ -"
    # Or: "+1/6+ +1/6+* Parry (6+)**"
    parts = profile_line.split()
    if len(parts) < 2:
        return None

    # Combat save
    combat = parts[0].rstrip('*')
    item['combatSave'] = combat if combat != '-' else None

    # Missile save
    missile_save = parts[1].rstrip('*')
    item['missileSave'] = missile_save if missile_save != '-' else None

    # Special rules (rest)
    special = ' '.join(parts[2:]).strip().rstrip('*')
    if special == '-':
        special = ''
    item['specialRules'] = special if special else None

    if notes_lines:
        item['notes'] = '\n'.join(notes_lines)

    return item


def name_to_key(name):
    """Convert a weapon/armour name to a camelCase key."""
    words = name.strip().lower().split()
    if not words:
        return ''
    # Handle slash names like "TWO/ADDITIONAL HAND WEAPONS"
    first = words[0].replace('/', '_')
    return first + ''.join(w.capitalize() for w in words[1:])


def extract_special_rules(pdf_path, start_page, end_page):
    """Extract common special rules with descriptions using column-aware extraction."""
    rules = {}

    with pdfplumber.open(pdf_path) as pdf:
        for pn in range(start_page - 1, min(end_page, len(pdf.pages))):
            page = pdf.pages[pn]
            lt, rt = extract_column_text(page)

            # Process each column sequentially
            for col_text in [lt, rt]:
                if not col_text:
                    continue
                col_text = col_text.replace('\u2019', "'").replace('\u2018', "'").replace('\u2013', '-')
                parse_rules_from_column(col_text, rules)

    return rules


def parse_rules_from_column(text, rules):
    """Parse special rule headings and descriptions from a single column of text."""
    lines = text.split('\n')

    # Find ALL-CAPS headings (rule names)
    # Rules look like: "ALWAYS STRIKES FIRST" or "ARMOUR PIERCING (*)"
    heading_pattern = re.compile(r'^([A-Z][A-Z\s\'\-]+(?:\s*\(\*\))?)$')

    headings = []
    for i, line in enumerate(lines):
        stripped = line.strip()
        if heading_pattern.match(stripped):
            # Skip section headers
            if stripped in ('SPECIAL RULES', 'SPECIAL', 'L RULES', 'RULES',
                           'CLOSE COMBAT', 'WEAPONS', 'MISSILE WEAPONS',
                           'DEPLOYMENT SPECIAL RULES', 'DEPLOYMENT SPECIAL',
                           'FORMATION SPECIAL RULES', 'FORMATION SPECIAL'):
                continue
            # Skip short fragments from column splits
            if len(stripped) <= 3:
                continue
            headings.append((i, stripped))

    # Extract description between headings
    for idx, (line_num, name) in enumerate(headings):
        start = line_num + 1
        end = headings[idx + 1][0] if idx + 1 < len(headings) else len(lines)

        description = '\n'.join(lines[start:end]).strip()
        # Clean page numbers
        description = re.sub(r'\n\s*\d{1,3}\s*$', '', description)

        # Check for parameterized rule (*)
        has_param = '(*)' in name
        clean_name = name.replace('(*)', '').strip()

        # Skip if empty/trivial
        if not description or len(description) < 10:
            continue

        key = name_to_key(clean_name)
        # If we already have this rule, append description (rule may span columns)
        if key in rules:
            rules[key]['description'] += '\n' + description
        else:
            rules[key] = {
                "name": clean_name,
                "parameterized": has_param,
                "description": description,
            }


def extract_deployment_and_formation_rules(pdf_path, start_page, end_page):
    """Extract deployment special rules (Ambushers, Scouts, Vanguard) and
    formation rules (Fast Cavalry, Skirmishers, Fly)."""
    deployment = {}
    formation = {}

    with pdfplumber.open(pdf_path) as pdf:
        current_section = None
        for pn in range(start_page - 1, min(end_page, len(pdf.pages))):
            page = pdf.pages[pn]
            lt, rt = extract_column_text(page)

            for col_text in [lt, rt]:
                if not col_text:
                    continue
                col_text = col_text.replace('\u2019', "'").replace('\u2018', "'").replace('\u2013', '-')

                # Detect section changes
                if 'DEPLOYMENT SPECIAL' in col_text[:50] or 'DEPLOYMENT\nSPECIAL' in col_text[:50]:
                    current_section = 'deployment'
                    # Strip the header
                    col_text = re.sub(r'^DEPLOYMENT\s*\n?SPECIAL\s*\n?RULES?\s*\n?', '', col_text).strip()
                elif 'FORMATION SPECIAL' in col_text[:50] or 'FORMATION\nSPECIAL' in col_text[:50]:
                    current_section = 'formation'
                    col_text = re.sub(r'^FORMATION\s*\n?SPECIAL\s*\n?RULES?\s*\n?', '', col_text).strip()
                # Stop at TROOP TYPES section (not special rules)
                elif 'TROOP' in col_text[:20] and ('TYPES' in col_text[:30] or 'P TYPES' in col_text[:30]):
                    current_section = None
                    continue

                if current_section:
                    target = deployment if current_section == 'deployment' else formation
                    parse_rules_from_column(col_text, target)

    return deployment, formation


def extract_shooting_to_hit(pdf_path, ranges):
    """Extract the BS-based shooting to-hit table."""
    table = {}
    if ranges.get('shooting_start'):
        text = extract_column_text_range(pdf_path,
                                         ranges['shooting_start'],
                                         ranges['shooting_start'] + 5)
        text = text.replace('\u2019', "'").replace('\u2018', "'")

        # Look for BS / Score to Hit table
        m = re.search(
            r'BS\s+.*?Score.*?Hit\s*\n((?:.*?\d\+.*?\n){1,12})',
            text, re.DOTALL
        )
        if m:
            for line in m.group(1).split('\n'):
                parts = re.findall(r'(\d+)\s+(\d)\+', line)
                for bs, score in parts:
                    table[f"BS{bs}"] = int(score)

    # Fallback: standard values
    if not table:
        table = {
            "BS1": 6, "BS2": 5, "BS3": 4, "BS4": 3, "BS5": 2,
            "BS6": 1, "BS7": 1, "BS8": 1, "BS9": 1, "BS10": 1,
        }

    return table


def extract_shooting_modifiers(pdf_path, ranges):
    """Extract shooting to-hit modifiers."""
    modifiers = {}
    if ranges.get('shooting_start'):
        text = extract_column_text_range(pdf_path,
                                         ranges['shooting_start'],
                                         ranges['shooting_start'] + 8)
        text = text.replace('\u2019', "'").replace('\u2018', "'")

        modifier_patterns = [
            (r'(?:Moving and Shooting|moved).*?(-\d+)', 'movingAndShooting'),
            (r'(?:Stand and Shoot|stand & shoot).*?(-\d+)', 'standAndShoot'),
            (r'(?:Long [Rr]ange).*?(-\d+)', 'longRange'),
            (r'(?:Soft [Cc]over).*?(-\d+)', 'softCover'),
            (r'(?:Hard [Cc]over).*?(-\d+)', 'hardCover'),
        ]
        for pattern, key in modifier_patterns:
            m = re.search(pattern, text)
            if m:
                modifiers[key] = int(m.group(1))

    return modifiers


def extract_core_rules(pdf_path):
    """Extract all core rules from the PDF."""
    print("Scanning PDF structure...")
    ranges = find_page_ranges(pdf_path)
    print(f"  Weapons: pages {ranges['weapons_start']}-{ranges['weapons_end']}")
    print(f"  Special rules: pages {ranges['special_rules_start']}-{ranges['special_rules_end']}")

    print("Extracting combat tables...")
    to_hit, to_wound, armour_mods = extract_tables_from_pdf(pdf_path)

    print("Extracting weapons and armour...")
    weapons, armour = extract_weapons_and_armour(
        pdf_path,
        ranges['weapons_start'] or 79,
        ranges['weapons_end'] or 82,
    )

    print("Extracting common special rules...")
    common_rules = extract_special_rules(
        pdf_path,
        ranges['special_rules_start'] or 58,
        ranges['special_rules_end'] or 68,
    )

    print("Extracting deployment and formation rules...")
    deployment_rules, formation_rules = extract_deployment_and_formation_rules(
        pdf_path,
        ranges['special_rules_end'] or 68,
        (ranges['special_rules_end'] or 68) + 5,
    )

    # Remove deployment/formation rules from common rules (they have their own sections)
    for key in list(common_rules.keys()):
        if key in deployment_rules or key in formation_rules:
            del common_rules[key]

    # Separate troop type descriptions from actual formation special rules
    actual_formation_rules = ['fastCavalry', 'skirmishers', 'fly']
    troop_types = {}
    for key in list(formation_rules.keys()):
        if key in actual_formation_rules:
            continue
        troop_types[key] = formation_rules.pop(key)

    print("Extracting shooting tables...")
    shooting_to_hit = extract_shooting_to_hit(pdf_path, ranges)
    shooting_modifiers = extract_shooting_modifiers(pdf_path, ranges)

    result = {
        "name": "Warhammer: The Game of Fantasy Battles - 9th Edition v2.32",
        "closeCombatToHit": {
            "description": "Roll a D6 for each attack. Compare WS of attacker and defender. Natural 1 always misses, natural 6 always hits.",
            "table": to_hit,
            "tableAxes": {"rows": "Attacker's WS (1-10)", "columns": "Target's WS (1-10)"},
        },
        "toWound": {
            "description": "Roll a D6 for each hit. Compare Strength of attacker with Toughness of defender. Natural 1 always fails, natural 6 always succeeds.",
            "table": to_wound,
            "tableAxes": {"rows": "Attacker's Strength (1-10)", "columns": "Target's Toughness (1-10)"},
        },
        "shootingToHit": {
            "description": "Roll a D6 for each shot. Score needed depends on BS.",
            "table": shooting_to_hit,
            "modifiers": shooting_modifiers,
        },
        "armourSaves": {
            "description": "Armour saves are modified by the Strength of the attack. Strength 4+ inflicts penalties.",
            # Deliberately not `armour_mods or {}` — an empty map reads as "no strength ever
            # modifies a save", which the app applies without complaint. validate() rejects it.
            "strengthModifiers": armour_mods,
        },
        "weapons": weapons,
        "armour": armour,
        "commonSpecialRules": common_rules,
        "deploymentSpecialRules": deployment_rules,
        "formationSpecialRules": formation_rules,
        "troopTypes": troop_types,
    }

    return result


def validate(result):
    """Return a list of reasons this extraction must not be shipped.

    Every check here corresponds to something the app dereferences on a hot path. wap-v3
    shipped with a null toWound.table and an empty strengthModifiers map: both produced
    structurally valid JSON, so nothing objected until the app crashed mid-render (the table)
    or silently stopped applying armour penalties (the map).
    """
    problems = []

    def check_square(table, label):
        if not table:
            problems.append(f'{label} is missing')
            return
        if len(table) != 10 or any(not row or len(row) != 10 for row in table):
            problems.append(f'{label} is not a full 10x10 grid (got {len(table)} rows)')
            return
        bad = [v for row in table for v in row if not 2 <= v <= 6]
        if bad:
            problems.append(f'{label} holds scores outside 2-6: {sorted(set(bad))}')

    check_square(result['closeCombatToHit']['table'], 'closeCombatToHit.table')
    check_square(result['toWound']['table'], 'toWound.table')

    mods = result['armourSaves']['strengthModifiers']
    if not mods:
        problems.append('armourSaves.strengthModifiers is empty — no attack would modify a save')
    else:
        missing = [f'S{s}' for s in range(1, 11) if f'S{s}' not in mods]
        if missing:
            problems.append(f'armourSaves.strengthModifiers is missing {", ".join(missing)}')

    for section, label in (('closeCombat', 'weapons.closeCombat'), ('missile', 'weapons.missile')):
        if not result['weapons'].get(section):
            problems.append(f'{label} is empty')

    if not result['armour']:
        problems.append('armour is empty')
    if not result['commonSpecialRules']:
        problems.append('commonSpecialRules is empty')

    return problems


def main():
    import argparse
    parser = argparse.ArgumentParser(description='Extract WAP core rules from PDF')
    parser.add_argument('pdf', help='Path to the core rules PDF')
    parser.add_argument('--output', default=None, help='Output JSON file path')
    parser.add_argument('--pretty', action='store_true', help='Pretty-print JSON')
    parser.add_argument('--force', action='store_true',
                        help='Write the output even if validation fails (for diagnosis only)')
    args = parser.parse_args()

    result = extract_core_rules(args.pdf)

    problems = validate(result)
    if problems:
        print('\nEXTRACTION INCOMPLETE — refusing to write:', file=sys.stderr)
        for problem in problems:
            print(f'  - {problem}', file=sys.stderr)
        print('\nThe app reads every one of these directly. Fix the extractor against this '
              'book\'s layout, or re-run with --force to inspect the partial output.',
              file=sys.stderr)
        if not args.force:
            return 1
        print('  (--force given: writing anyway)', file=sys.stderr)

    json_str = json.dumps(result, indent=2 if args.pretty else None, ensure_ascii=False)

    if args.output:
        with open(args.output, 'w', encoding='utf-8') as f:
            f.write(json_str)

    # Print summary
    th = result['closeCombatToHit']['table']
    tw = result['toWound']['table']
    print(f"\nCore rules extracted{' -> ' + args.output if args.output else ''}")
    print(f"  To-Hit table: {'OK' if th else 'MISSING'} ({len(th or [])} rows)")
    print(f"  To-Wound table: {'OK' if tw else 'MISSING'} ({len(tw or [])} rows)")
    print(f"  Shooting To-Hit: {len(result['shootingToHit']['table'])} entries")
    print(f"  Armour modifiers: {len(result['armourSaves']['strengthModifiers'] or {})} entries")
    print(f"  Close combat weapons: {len(result['weapons']['closeCombat'])}")
    print(f"  Missile weapons: {len(result['weapons']['missile'])}")
    print(f"  Armour types: {len(result['armour'])}")
    print(f"  Common special rules: {len(result['commonSpecialRules'])}")
    print(f"  Deployment rules: {len(result.get('deploymentSpecialRules', {}))}")
    print(f"  Formation rules: {len(result.get('formationSpecialRules', {}))}")

    # List weapon names for verification
    print(f"\n  CC weapons: {', '.join(sorted(result['weapons']['closeCombat'].keys()))}")
    print(f"  Missile weapons: {', '.join(sorted(result['weapons']['missile'].keys()))}")
    print(f"  Armour: {', '.join(sorted(result['armour'].keys()))}")
    print(f"\n  Special rules: {', '.join(sorted(result['commonSpecialRules'].keys()))}")
    if result.get('deploymentSpecialRules'):
        print(f"  Deployment: {', '.join(sorted(result['deploymentSpecialRules'].keys()))}")
    if result.get('formationSpecialRules'):
        print(f"  Formation: {', '.join(sorted(result['formationSpecialRules'].keys()))}")

    if not args.output:
        print(json_str)

    return 0


if __name__ == '__main__':
    sys.exit(main() or 0)
