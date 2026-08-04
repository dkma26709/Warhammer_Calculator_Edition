"""
Extract WAP army book data directly from PDF files into structured JSON.
Uses pdfplumber for text extraction, then regex-based parsing for structure.
"""

import re
import json
import sys
import pdfplumber


def extract_text_from_pdf(pdf_path):
    """Extract all text from a PDF, page by page."""
    pages = []
    with pdfplumber.open(pdf_path) as pdf:
        for page in pdf.pages:
            text = page.extract_text()
            if text:
                # Strip trailing page number (standalone number on last line)
                text = re.sub(r'\n\s*\d{1,3}\s*$', '', text.strip())
                pages.append(text)
    return pages


def normalize_bullets(text):
    """Normalize bullet characters (pdfplumber often produces replacement chars)."""
    # Replace replacement character and various bullet types with a standard bullet
    text = re.sub(r'[�\u2022\u2023\u25E6\u2043\u2219\u00B7]', '•', text)
    return text


SECTION_HEADERS = {
    'LORDS', 'HEROES', 'CORE UNITS', 'SPECIAL UNITS', 'RARE UNITS',
    'NAMED CHARACTERS', 'SPECIAL CHARACTERS',
    'SPECIAL CHARACTERS (LORDS)', 'SPECIAL CHARACTERS (HEROES)',
    'CHARACTER MOUNTS',
}

# Multi-line section headers that appear split across lines in PDFs
MULTI_LINE_SECTIONS = {
    ('SPECIAL CHARACTERS', '(LORDS)'): 'SPECIAL CHARACTERS (LORDS)',
    ('SPECIAL CHARACTERS', '(HEROES)'): 'SPECIAL CHARACTERS (HEROES)',
}


def preprocess_text(text):
    """Fix known multi-line section headers and other PDF artifacts."""
    # Rejoin split section headers: "SPECIAL CHARACTERS\n(LORDS)" -> single line
    for (line1, line2), combined in MULTI_LINE_SECTIONS.items():
        text = re.sub(
            rf'^{re.escape(line1)}\s*\n\s*{re.escape(line2)}\s*$',
            combined,
            text,
            flags=re.MULTILINE
        )
    return text


def is_all_caps_title(text):
    """Check if a line looks like an ALL-CAPS unit/section title."""
    return bool(re.match(r"^[A-Z][A-Z\s'\-&/()\u2019]+$", text.strip()))


def find_unit_title_before(full_text, pos):
    """Scan backwards from pos to find the ALL-CAPS unit title.
    Skips subtitle lines (mixed case), section headers, blank lines.
    Returns (title, title_position) or (None, pos)."""
    pre_text = full_text[:pos].rstrip()
    lines_before = pre_text.split('\n')

    # Scan back up to 8 lines (to handle subtitle + section header + blank lines)
    for j in range(len(lines_before) - 1, max(len(lines_before) - 8, -1), -1):
        candidate = lines_before[j].strip()
        if not candidate:
            continue
        if is_all_caps_title(candidate) and candidate not in SECTION_HEADERS:
            title_pos = pre_text.rfind(candidate)
            return candidate, title_pos
        # Skip non-caps lines (subtitles like "Lord of the Solar-City...")
        # and section headers (which we want to skip past)
        if candidate in SECTION_HEADERS:
            continue  # keep looking past section headers

    return None, pos


def find_unit_boundaries(full_text):
    """Find start/end positions of each unit block using Profile headers."""
    profile_header = re.compile(
        r'^Profile\s+M\s+WS\s+BS\s+S\s+T\s+W\s+I\s+A\s+Ld\s+Points?\s*$',
        re.MULTILINE
    )

    matches = list(profile_header.finditer(full_text))
    blocks = []

    for i, m in enumerate(matches):
        unit_title, title_pos = find_unit_title_before(full_text, m.start())

        if not unit_title:
            unit_title = "UNKNOWN"
            title_pos = m.start()

        start = title_pos

        # End is the start of the next unit's title (or end of text)
        if i + 1 < len(matches):
            next_title, next_title_pos = find_unit_title_before(full_text, matches[i + 1].start())
            end = next_title_pos if next_title else matches[i + 1].start()
        else:
            end = len(full_text)

        block_text = full_text[start:end].strip()
        blocks.append((title_case(unit_title), block_text))

    return blocks


def title_case(name):
    """Convert ALL-CAPS unit name to title case, preserving apostrophes."""
    # Special handling for short words that should stay lowercase
    small_words = {'of', 'the', 'and', 'in', 'on', 'at', 'to', 'for', 'with'}
    words = name.strip().split()
    result = []
    for i, w in enumerate(words):
        lower = w.lower()
        if i > 0 and lower in small_words:
            result.append(lower)
        else:
            result.append(w.capitalize())
    return ' '.join(result)


def parse_stat_value(val):
    """Parse a stat value: integer, None for '-', or '*' for variable."""
    val = val.strip()
    if val in ('-', '–', ''):
        return None
    if val == '*':
        return '*'
    try:
        return int(val)
    except ValueError:
        return val


def parse_stat_line(line):
    """Parse a stat line into a profile dict. Returns (profile, has_points) or None."""
    # With points (11 groups: name + 9 stats + points, points can be decimal)
    m = re.match(
        r'^(.+?)\s+([\d\-\*]+)\s+([\d\-\*]+)\s+([\d\-\*]+)\s+([\d\-\*]+)\s+([\d\-\*]+)\s+([\d\-\*]+)\s+([\d\-\*]+)\s+([\d\-\*]+)\s+([\d\-\*]+)\s+([\d.]+)\s*$',
        line
    )
    if m:
        pts = float(m.group(11))
        return {
            "name": m.group(1).strip(),
            "M": parse_stat_value(m.group(2)),
            "WS": parse_stat_value(m.group(3)),
            "BS": parse_stat_value(m.group(4)),
            "S": parse_stat_value(m.group(5)),
            "T": parse_stat_value(m.group(6)),
            "W": parse_stat_value(m.group(7)),
            "I": parse_stat_value(m.group(8)),
            "A": parse_stat_value(m.group(9)),
            "Ld": parse_stat_value(m.group(10)),
            "points": int(pts) if pts == int(pts) else pts,
        }, True

    # Without points (10 groups: name + 9 stats) — mounts, crew
    # Optional trailing dash/en-dash handles the '-' placeholder in the Points column
    m = re.match(
        r'^(.+?)\s+([\d\-\*]+)\s+([\d\-\*]+)\s+([\d\-\*]+)\s+([\d\-\*]+)\s+([\d\-\*]+)\s+([\d\-\*]+)\s+([\d\-\*]+)\s+([\d\-\*]+)\s+([\d\-\*]+)\s*(?:[-–]\s*)?$',
        line
    )
    if m:
        return {
            "name": m.group(1).strip(),
            "M": parse_stat_value(m.group(2)),
            "WS": parse_stat_value(m.group(3)),
            "BS": parse_stat_value(m.group(4)),
            "S": parse_stat_value(m.group(5)),
            "T": parse_stat_value(m.group(6)),
            "W": parse_stat_value(m.group(7)),
            "I": parse_stat_value(m.group(8)),
            "A": parse_stat_value(m.group(9)),
            "Ld": parse_stat_value(m.group(10)),
        }, False

    return None


# All known field labels that appear in unit blocks
FIELD_LABELS = [
    "UNIT SIZE", "TROOP TYPE", "BASE SIZE", "EQUIPMENT",
    "MAGIC", "OPTIONS", "SPECIAL RULES", "UPGRADES",
    "NOTES", "CREW", "MOUNT", "DRAWN BY",
]

# Regex lookahead for field boundaries
FIELD_BOUNDARY = '|'.join(re.escape(f) for f in FIELD_LABELS)
FIELD_PATTERN = re.compile(
    rf'(?P<label>{FIELD_BOUNDARY}):\s*(?P<value>.*?)(?=\n(?:{FIELD_BOUNDARY}):|\Z)',
    re.DOTALL | re.IGNORECASE
)


def extract_fields(text):
    """Extract all labelled fields from a unit block."""
    fields = {}
    for m in FIELD_PATTERN.finditer(text):
        label = m.group('label').upper().strip()
        value = m.group('value').strip()
        # Clean trailing page numbers and section bleed
        value = clean_trailing(value)
        if value:
            fields[label] = value
    return fields


def clean_trailing(text):
    """Remove trailing page numbers, section headers, and noise."""
    if not text:
        return text
    # Remove trailing page numbers (standalone digits on last line)
    text = re.sub(r'\n\s*\d{1,3}\s*$', '', text.strip())
    text = re.sub(r'\n\s*\d{1,3}\s*$', '', text.strip())
    # Remove trailing section headers that bleed in
    section_headers = r'(?:LORDS|HEROES|CORE UNITS?|SPECIAL UNITS?|RARE UNITS?|NAMED CHARACTERS|SPECIAL CHARACTERS(?:\s*\((?:LORDS|HEROES)\))?|CHARACTER MOUNTS|CREDITS)'
    text = re.sub(rf'\n\s*{section_headers}\s*$', '', text.strip(), flags=re.IGNORECASE)
    # Repeat in case there are multiple trailing lines
    text = re.sub(rf'\n\s*{section_headers}\s*$', '', text.strip(), flags=re.IGNORECASE)
    return text.strip()


def parse_cost(text):
    """Extract point cost and per_model flag from a cost string.
    Examples: '+3 points', '+1 point/model', 'free', '+0.5 point/model'
    Returns (cost, per_model) or (None, False)."""
    text = text.strip()
    if 'free' in text.lower():
        return 0, '/model' in text.lower()
    m = re.search(r'[+]?\s*([\d.]+)\s*points?(?:/model)?', text)
    if m:
        cost = float(m.group(1))
        if cost == int(cost):
            cost = int(cost)
        per_model = '/model' in text
        return cost, per_model
    return None, False


def parse_limit(text):
    """Extract a 'worth up to X points' limit. Returns int or None."""
    m = re.search(r'up to (?:a total of )?(\d+)\s*points', text)
    return int(m.group(1)) if m else None


# Faction upgrade families that share a character's magic item allowance, e.g.
# "May take Blessed Spawnings and/or Magic Items up to a total of 50 points".
# Keys match the factionUpgrades categories emitted by extract_army_extras.py.
UPGRADE_FAMILIES = [
    (r'blessed\s+spawnings?', 'blessed_spawnings'),
    (r'disciplines?\s+of\s+the\s+old\s+ones', 'disciplines'),
    (r'gifts?\s+of\s+khaine', 'gifts_of_khaine'),
    (r'gifts?\s+of\s+chaos', 'gifts_of_chaos'),
    (r'daemonic\s+gifts?', 'daemonic_gifts'),
    (r'vampiric\s+powers?', 'vampiric_powers'),
    (r'big\s+names?', 'big_names'),
    (r'knightly\s+orders?', 'knightly_orders'),
    (r'mutations?\s+&\s+powers?', 'mutations_and_powers'),
]


def match_upgrade_family(text):
    """Map an upgrade family named in option text to its factionUpgrades category."""
    if not text:
        return None
    for pattern, category in UPGRADE_FAMILIES:
        if re.search(pattern, text, re.IGNORECASE):
            return category
    return None


def classify_option(text):
    """Classify and structure a single option line (primary bullet).
    Returns a structured dict."""
    raw = text.strip()

    # --- Command group ---
    m = re.match(r'May upgrade one .+ to a (Leader|Musician|Standard Bearer)\s+(.*)', raw, re.IGNORECASE)
    if m:
        role = m.group(1).strip()
        cost, per_model = parse_cost(m.group(2))
        return {"type": "command", "role": role, "points": cost, "raw": raw}

    # --- Magic Standard ---
    m = re.match(r'May take a Magic Standard (.*)', raw, re.IGNORECASE)
    if m:
        limit = parse_limit(m.group(1))
        return {"type": "magic_standard", "pointsLimit": limit, "raw": raw}

    # --- Magic Items, optionally sharing the allowance with a faction upgrade family ---
    m = re.match(
        r'May take (?:(.+?) and/or )?Magic Items(?: and/or (.+?))?\s*up to.*?(\d+)\s*points',
        raw, re.IGNORECASE)
    if m:
        option = {"type": "magic_items", "pointsLimit": int(m.group(3)), "raw": raw}
        # Characters get their upgrade family via the shared allowance wording; record it so
        # parse_options can emit a companion option the army builder can render.
        category = match_upgrade_family(m.group(1) or m.group(2))
        if category:
            option["factionUpgrade"] = category
        return option

    # --- Blessed Spawning (standalone, per-model) ---
    m = re.match(r'May take a Blessed Spawning\s+(.*)', raw, re.IGNORECASE)
    if m:
        cost, per_model = parse_cost(m.group(1))
        return {"type": "blessed_spawning", "points": cost, "perModel": per_model, "raw": raw}

    # --- Battle Standard ---
    if re.search(r'carry the Battle Standard', raw, re.IGNORECASE):
        cost, _ = parse_cost(raw)
        return {"type": "battle_standard", "points": cost, "raw": raw}

    # --- Wizard upgrade ---
    m = re.match(r'May be upgraded to (?:a )?(Level \d+ Wizard)\s+(.*)', raw, re.IGNORECASE)
    if m:
        cost, _ = parse_cost(m.group(2))
        return {"type": "wizard_upgrade", "upgrade": m.group(1), "points": cost, "raw": raw}

    # --- General upgrade (Skirmishers, Ambushers, Inner Circle, Knightly Order, etc.) ---
    m = re.match(r'May be upgraded to (?:a )?(.+)', raw, re.IGNORECASE)
    if m:
        upgrade_text = m.group(1).strip()
        name_part = re.split(r'\s+[+]?\s*[\d.]+\s*points?|\s+free\s*$|\s+no points', upgrade_text)[0].strip()
        cost, per_model = parse_cost(upgrade_text)
        return {"type": "upgrade", "upgrade": name_part, "points": cost, "perModel": per_model, "raw": raw}

    # --- Mount choice (choose one of the following) ---
    m = re.match(r'(?:May|Must) be mounted (?:on|upon) (?:one of the following)', raw, re.IGNORECASE)
    if m:
        return {"type": "mount", "action": "choose", "choices": [], "raw": raw}

    # --- Mount single: "May be mounted on a Terradon +28 points" or "May be mounted upon X..." ---
    m = re.match(r'(?:May|Must) be mounted (?:on|upon) (?:the |a )?(.+)', raw, re.IGNORECASE)
    if m:
        mount_text = m.group(1)
        name_part = re.split(r'\s+[+]\s*[\d.]', mount_text)[0].strip()
        cost, _ = parse_cost(mount_text)
        return {"type": "mount", "action": "choose", "choices": [
            {"name": name_part, "points": cost}
        ], "raw": raw}

    # --- Weapon/equipment replace with choices (May replace / May exchange) ---
    m = re.match(r'May (?:replace|exchange) (.+?) (?:with|for) (?:one of the following)', raw, re.IGNORECASE)
    if m:
        replaced = normalize_equipment_name(m.group(1).strip())
        return {"type": "equipment", "action": "replace", "replaces": replaced, "choices": [], "raw": raw}

    # --- Weapon/equipment replace single ---
    m = re.match(r'May (?:replace|exchange) (.+?) (?:with|for) (.+)', raw, re.IGNORECASE)
    if m:
        replaced = normalize_equipment_name(m.group(1).strip())
        new_item_text = m.group(2).strip()
        name_part = re.split(r'\s+[+]?\s*\d+\s*points?|\s+free', new_item_text)[0].strip()
        cost, per_model = parse_cost(new_item_text)
        return {"type": "equipment", "action": "replace", "replaces": replaced,
                "choices": [{"name": normalize_equipment_name(name_part), "points": cost, "perModel": per_model}],
                "raw": raw}

    # --- Include model: "May include one Fanatic for every 10 models..." ---
    m = re.match(r'May include (.+)', raw, re.IGNORECASE)
    if m:
        cost, per_model = parse_cost(m.group(1))
        return {"type": "unit_addition", "raw": raw}

    # --- Special unit upgrades: "May upgrade two X to a Y for every N models..." ---
    m = re.match(r'May upgrade (?:one|two|three|\d+) .+ to (?:a )?(.+?)(?:\s+for every\b)', raw, re.IGNORECASE)
    if m:
        return {"type": "unit_addition", "raw": raw}

    # --- Other unique upgrades: "May upgrade one X to a Jester..." ---
    m = re.match(r'May upgrade one .+ to (?:a |an )?(?!Leader|Musician|Standard Bearer)(.+)', raw, re.IGNORECASE)
    if m:
        upgrade_text = m.group(1).strip()
        name_part = re.split(r'\s+[+]?\s*[\d.]+\s*points?|\s+free\s*$', upgrade_text)[0].strip()
        cost, per_model = parse_cost(upgrade_text)
        return {"type": "unit_addition", "upgrade": name_part, "points": cost, "raw": raw}

    # --- Weapon/equipment choose one (May/Must/Any model) ---
    m = re.match(r'(?:May|Must|Any model may)\s+(?:choose|pick|be armed with)\s+(?:one|any|at least one|up to \w+)\s+of the following', raw, re.IGNORECASE)
    if m:
        return {"type": "equipment", "action": "choose", "choices": [], "raw": raw}

    # --- Simple equipment add: "May take spears +1 point/model" ---
    m = re.match(r'May take (.+)', raw, re.IGNORECASE)
    if m:
        item_text = m.group(1).strip()
        # Split name from cost
        name_part = re.split(r'\s+[+]\s*[\d.]|\s+free', item_text)[0].strip()
        cost, per_model = parse_cost(item_text)
        return {"type": "equipment", "action": "add",
                "choices": [{"name": normalize_equipment_name(name_part), "points": cost, "perModel": per_model}],
                "raw": raw}

    # --- Additional model: "May take an additional Skink +5 points/model" ---
    m = re.match(r'May take an additional (.+)', raw, re.IGNORECASE)
    if m:
        cost, per_model = parse_cost(m.group(1))
        return {"type": "additional_model", "points": cost, "perModel": per_model, "raw": raw}

    # --- May have (alternative to "May take") ---
    m = re.match(r'May have (.+)', raw, re.IGNORECASE)
    if m:
        item_text = m.group(1).strip()
        name_part = re.split(r'\s+[+]\s*[\d.]|\s+free\s*$', item_text)[0].strip()
        cost, per_model = parse_cost(item_text)
        return {"type": "equipment", "action": "add",
                "choices": [{"name": normalize_equipment_name(name_part), "points": cost, "perModel": per_model}],
                "raw": raw}

    # --- Bare name with cost: "Brace of pistols free", "Repeater handgun +5 points",
    #     "Armour Piercing (1) +1 point/model", "Implacable free" ---
    m = re.match(r'^(.+?)\s+(?:[+]?\s*[\d.]+\s*points?(?:/model)?|free)\s*$', raw, re.IGNORECASE)
    if m:
        name_part = m.group(1).strip()
        cost, per_model = parse_cost(raw)
        return {"type": "equipment", "action": "add",
                "choices": [{"name": normalize_equipment_name(name_part), "points": cost, "perModel": per_model}],
                "raw": raw}

    # --- Fallback ---
    return {"type": "other", "raw": raw}


def normalize_equipment_name(name):
    """Normalize equipment names for consistent matching across units.
    Strips articles, pluralizes/singularizes to canonical form."""
    name = name.strip().rstrip('.')
    # Remove leading articles
    name = re.sub(r'^(?:an?\s+|the\s+)', '', name, flags=re.IGNORECASE)
    # Canonical lowercase for matching
    lower = name.lower()

    # Map common variations to canonical names
    canonical = {
        # Melee weapons
        'spear': 'spear', 'spears': 'spear',
        'pike': 'pike', 'pikes': 'pike',
        'shield': 'shield', 'shields': 'shield',
        'buckler': 'buckler', 'bucklers': 'buckler',
        'great weapon': 'great weapon', 'great weapons': 'great weapon',
        'additional hand weapon': 'additional hand weapon',
        'additional hand weapons': 'additional hand weapon',
        'two hand weapons': 'additional hand weapon',
        'hand weapon': 'hand weapon', 'hand weapons': 'hand weapon',
        'light lance': 'light lance', 'light lances': 'light lance',
        'lance': 'lance', 'lances': 'lance',
        'polearm': 'polearm', 'polearms': 'polearm',
        'halberd': 'halberd', 'halberds': 'halberd',
        'morning star': 'morning star', 'morning stars': 'morning star',
        'flail': 'flail', 'flails': 'flail',
        'war gauntlets': 'war gauntlets',
        # Armour
        'light armour': 'light armour',
        'medium armour': 'medium armour',
        'heavy armour': 'heavy armour',
        'full plate armour': 'full plate armour',
        'barding': 'barding',
        # Ranged weapons
        'blowpipe': 'blowpipe', 'blowpipes': 'blowpipe',
        'shortbow': 'shortbow', 'shortbows': 'shortbow',
        'bow': 'bow', 'bows': 'bow',
        'longbow': 'longbow', 'longbows': 'longbow',
        'crossbow': 'crossbow', 'crossbows': 'crossbow',
        'sling': 'sling', 'slings': 'sling',
        'javelin': 'javelin', 'javelins': 'javelin',
        'throwing weapon': 'throwing weapon', 'throwing weapons': 'throwing weapon',
        'pistol': 'pistol', 'pistols': 'pistol',
        'brace of pistols': 'brace of pistols',
        'handgun': 'handgun', 'handguns': 'handgun',
        'repeater handgun': 'repeater handgun',
        'repeater pistol': 'repeater pistol',
        'hochland long rifle': 'hochland long rifle',
        'grenade launching blunderbuss': 'grenade launching blunderbuss',
        'fireleech bolas': 'fireleech bolas',
    }
    return canonical.get(lower, name)


def parse_sub_choice(text):
    """Parse a sub-option line into a choice dict.
    Handles: 'Shields +1 point/model', 'Replace spears with pikes +1.5 points/model',
    'Slings & shields free', etc."""
    raw = text.strip()

    # Handle "Replace X with Y +cost" as a sub-choice
    m = re.match(r'Replace (.+?) with (.+)', raw, re.IGNORECASE)
    if m:
        replaced = normalize_equipment_name(m.group(1).strip())
        new_text = m.group(2).strip()
        name_part = re.split(r'\s+[+]\s*[\d.]|\s+free\s*$', new_text)[0].strip()
        cost, per_model = parse_cost(new_text)
        return {"name": normalize_equipment_name(name_part), "replaces": replaced,
                "points": cost, "perModel": per_model}

    cost, per_model = parse_cost(raw)
    # Name is everything before the cost
    name_part = re.split(r'\s+[+]\s*[\d.]|\s+free\s*$', raw)[0].strip()
    # Handle combined items: "Slings & shields", "Javelins & shields"
    if ' & ' in name_part:
        parts = [normalize_equipment_name(p.strip()) for p in name_part.split(' & ')]
        return {"name": parts, "points": cost, "perModel": per_model}
    return {"name": normalize_equipment_name(name_part), "points": cost, "perModel": per_model}


def has_following_choices(text):
    """Check if a line ends with 'one of the following:' or similar."""
    return bool(re.search(r'(?:one |any |at least one |up to \w+ )?of the following\s*:?\s*$', text, re.IGNORECASE))


def parse_options(options_text):
    """Parse options text into a list of structured option dicts.

    Bullet hierarchy:
    • = primary option
    - = sub-option of the preceding •, OR sub-choice under a 'choose' •
    When a - line says 'one of the following:', subsequent • bullets are its sub-choices.
    """
    if not options_text:
        return []

    options = []
    lines = options_text.split('\n')

    # First pass: tokenize into (bullet_type, text) pairs
    tokens = []
    for line in lines:
        line = line.strip()
        if not line:
            continue
        if re.match(r'^[•]\s+', line):
            tokens.append(('bullet', re.sub(r'^[•]\s+', '', line)))
        elif re.match(r'^[-–]\s+', line):
            tokens.append(('dash', re.sub(r'^[-–]\s+', '', line)))
        else:
            tokens.append(('cont', line))

    # Second pass: build structured options
    i = 0
    current_primary = None  # The current • option being built
    # When a dash introduces "one of the following:", we track it so subsequent
    # • bullets get attached as its sub-choices
    dash_with_choices = None

    while i < len(tokens):
        btype, text = tokens[i]

        # Continuation line
        if btype == 'cont':
            if current_primary:
                current_primary['raw'] = current_primary.get('raw', '') + ' ' + text
            i += 1
            continue

        # If we're collecting •-bullets as sub-choices for a dash group
        if dash_with_choices is not None and btype == 'bullet':
            # Stop collecting if this bullet looks like a new primary option
            # (starts with "May" — indicates a new action, not a bare choice name)
            if re.match(r'May\s+', text, re.IGNORECASE):
                options.append(dash_with_choices)
                dash_with_choices = None
                # Fall through to handle as a normal bullet below
            else:
                dash_with_choices['choices'].append(parse_sub_choice(text))
                i += 1
                continue

        # End any active dash_with_choices collection when we hit a non-bullet
        if dash_with_choices is not None:
            options.append(dash_with_choices)
            dash_with_choices = None

        if btype == 'bullet':
            # Flush previous primary
            if current_primary:
                options.append(current_primary)
            current_primary = classify_option(text)
            i += 1

        elif btype == 'dash':
            # Dash line: could be sub-choice, sub-option, or introduce a new group
            if has_following_choices(text):
                # This dash introduces a choice group — following • bullets are its choices
                if current_primary:
                    options.append(current_primary)
                    current_primary = None
                dash_with_choices = classify_option(text)
                i += 1
            elif current_primary and 'choices' in current_primary:
                # Sub-choice of current primary (e.g. under "choose one of the following:")
                current_primary['choices'].append(parse_sub_choice(text))
                i += 1
            else:
                # Sub-option of the preceding primary (e.g. Leader's pistol,
                # Standard Bearer's magic standard)
                sub = classify_option(text)
                if current_primary:
                    # Attach as a sub-option
                    if 'subOptions' not in current_primary:
                        current_primary['subOptions'] = []
                    current_primary['subOptions'].append(sub)
                else:
                    options.append(sub)
                i += 1

    # Flush remaining
    if dash_with_choices is not None:
        options.append(dash_with_choices)
    if current_primary:
        options.append(current_primary)

    return expand_shared_allowance_upgrades(options)


def expand_shared_allowance_upgrades(options):
    """Split a shared "upgrades and/or Magic Items" allowance into two options.

    The allowance stays a magic_items option; the upgrade family becomes its own
    faction_upgrade option so the builder can offer the picks. Each pick costs the
    points listed against it in the faction's extras, so no points go on the option.
    """
    expanded = []
    for option in options:
        category = option.pop('factionUpgrade', None) if isinstance(option, dict) else None
        expanded.append(option)
        if category:
            expanded.append({
                "type": "faction_upgrade",
                "factionUpgrade": category,
                "raw": option.get('raw', ''),
            })
    return expanded


def detect_section(full_text, unit_start_pos):
    """Determine which army list section a unit belongs to based on position."""
    section_pattern = re.compile(
        r'^(LORDS|HEROES|CORE UNITS?|SPECIAL UNITS?|RARE UNITS?|'
        r'NAMED CHARACTERS|SPECIAL CHARACTERS\s*(?:\(LORDS\)|\(HEROES\))?|'
        r'CHARACTER MOUNTS)\s*$',
        re.MULTILINE
    )

    category = "Unknown"
    for m in section_pattern.finditer(full_text):
        if m.start() < unit_start_pos:
            raw = m.group(1).strip()
            category = normalize_category(raw)
        else:
            break
    return category


def normalize_category(cat):
    """Normalize section category names."""
    cat = cat.strip()
    mapping = {
        "LORDS": "Lords",
        "HEROES": "Heroes",
        "CORE UNITS": "Core",
        "CORE": "Core",
        "SPECIAL UNITS": "Special",
        "SPECIAL": "Special",
        "RARE UNITS": "Rare",
        "RARE": "Rare",
        "NAMED CHARACTERS": "Named Characters",
        "SPECIAL CHARACTERS": "Named Characters",
        "SPECIAL CHARACTERS (LORDS)": "Named Characters",
        "SPECIAL CHARACTERS (HEROES)": "Named Characters",
    }
    return mapping.get(cat, cat)


def split_special_rules(raw_text):
    """Split special rules into rule names and detailed descriptions.
    The first line typically has comma-separated rule names.
    Subsequent lines (especially bullet-pointed) are descriptions.
    Returns (rule_names_string, descriptions_list_or_None)."""
    if not raw_text:
        return None, None

    lines = raw_text.split('\n')
    # First line: comma-separated rule names
    rule_names = lines[0].strip()

    # Remaining lines: descriptions (bullet points, paragraphs)
    descriptions = []
    current_desc = None
    for line in lines[1:]:
        line = line.strip()
        if not line:
            continue
        if re.match(r'^[•]\s+', line):
            if current_desc:
                descriptions.append(current_desc)
            current_desc = re.sub(r'^[•]\s+', '', line)
        else:
            # Continuation of previous description
            if current_desc:
                current_desc += ' ' + line
            else:
                current_desc = line

    if current_desc:
        descriptions.append(current_desc)

    return rule_names, descriptions if descriptions else None


def parse_unit_block(unit_title, block_text, category):
    """Parse a single unit block into structured data."""
    # Extract stat lines (lines right after the Profile header)
    profiles = []
    after_header = re.split(r'Profile\s+M\s+WS\s+BS\s+S\s+T\s+W\s+I\s+A\s+Ld\s+Points?\s*\n', block_text)

    if len(after_header) > 1:
        stat_section = after_header[1]
        for line in stat_section.split('\n'):
            line = line.strip()
            if not line:
                continue
            # Stop at first field label
            if re.match(rf'^({FIELD_BOUNDARY}):', line, re.IGNORECASE):
                break
            result = parse_stat_line(line)
            if result:
                profiles.append(result)
            elif profiles:
                break  # Non-stat line after stats means we're done

    # Extract labelled fields
    fields = extract_fields(block_text)

    # Parse options specifically
    options = parse_options(fields.get("OPTIONS"))

    # Split special rules into rule names vs detailed descriptions
    raw_special_rules = fields.get("SPECIAL RULES")
    rule_names, rule_details = split_special_rules(raw_special_rules)

    # Build unit dict
    unit = {
        "name": unit_title,
        "category": category,
        "profiles": [p for p, _ in profiles],
        "troopType": fields.get("TROOP TYPE"),
        "baseSize": fields.get("BASE SIZE"),
        "equipment": fields.get("EQUIPMENT"),
        "magic": fields.get("MAGIC"),
        "options": options,
        "specialRules": rule_names,
        "specialRuleDescriptions": rule_details if rule_details else None,
        "upgrades": fields.get("UPGRADES"),
        "notes": fields.get("NOTES"),
        "unitSize": fields.get("UNIT SIZE"),
        "crew": fields.get("CREW"),
        "mount": fields.get("MOUNT"),
        "drawnBy": fields.get("DRAWN BY"),
        "pointsCost": None,
    }

    # Set points cost from first profile with points
    for p, has_pts in profiles:
        if has_pts and "points" in p:
            unit["pointsCost"] = p["points"]
            break

    return unit


def extract_army(pdf_path, faction_name, unit_filter=None):
    """Extract all units (or a filtered subset) from a WAP army book PDF."""
    pages = extract_text_from_pdf(pdf_path)
    full_text = preprocess_text(normalize_bullets('\n'.join(pages)))

    # Find unit boundaries
    blocks = find_unit_boundaries(full_text)

    units = []
    for unit_title, block_text in blocks:
        # Apply filter if specified
        if unit_filter and unit_filter.upper() not in unit_title.upper():
            continue

        category = detect_section(full_text, full_text.index(block_text))
        unit = parse_unit_block(unit_title, block_text, category)
        units.append(unit)

    return {
        "factionName": faction_name,
        "units": units,
    }


def main():
    import argparse
    parser = argparse.ArgumentParser(description='Extract WAP army book data from PDF')
    parser.add_argument('pdf', help='Path to the PDF file')
    parser.add_argument('--faction', default='Unknown', help='Faction name')
    parser.add_argument('--unit', default=None, help='Filter to a specific unit name (partial match)')
    parser.add_argument('--output', default=None, help='Output JSON file path')
    parser.add_argument('--pretty', action='store_true', help='Pretty-print JSON output')
    args = parser.parse_args()

    result = extract_army(args.pdf, args.faction, args.unit)

    json_str = json.dumps(result, indent=2 if args.pretty else None, ensure_ascii=False)

    if args.output:
        with open(args.output, 'w', encoding='utf-8') as f:
            f.write(json_str)
        print(f"Extracted {len(result['units'])} units -> {args.output}")
    else:
        print(json_str)


if __name__ == '__main__':
    main()
