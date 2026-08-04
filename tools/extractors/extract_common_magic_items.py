"""Extract the Common Magic Items list from the WAP rulebook PDF.

Produces the JSON consumed at data/{system}/common-magic-items.json:
a flat list of {name, points, category, description} entries.

The PDF uses a two-column layout, so text is read column-by-column
(same approach as extract_core_rules.py).
"""
import json
import re
import pdfplumber


CATEGORY_HEADERS = {
    "MAGIC WEAPONS": "magic_weapon",
    "MAGIC ARMOUR": "magic_armour",
    "TALISMANS": "talisman",
    "ARCANE ITEMS": "arcane_item",
    "ENCHANTED ITEMS": "enchanted_item",
    "MAGIC STANDARDS": "magic_standard",
}

# "GIANT BLADE 45 points" / "WHITE CLOAK * 25 points"
ITEM_PATTERN = re.compile(
    r"^([A-Z][A-Z'’&\-\. ]+?)\s*(\*)?\s+(\d+)\s*points?\s*$"
)

SMALL_WORDS = {"of", "the", "and", "with"}


def title_case(name):
    words = name.title().split()
    return " ".join(
        w.lower() if i > 0 and w.lower() in SMALL_WORDS else w
        for i, w in enumerate(words)
    )


def extract_column_text(page):
    """Extract text from a page reading left column then right column."""
    mid = page.width / 2
    left = page.crop((0, 0, mid, page.height))
    right = page.crop((mid, 0, page.width, page.height))
    parts = []
    for col in (left, right):
        text = col.extract_text()
        if text:
            parts.append(re.sub(r"\n\s*\d{1,3}\s*$", "", text.strip()))
    return "\n".join(parts)


def find_section_pages(pdf):
    """Locate the page range of the magic items chapter."""
    start = end = None
    for i, page in enumerate(pdf.pages):
        text = page.extract_text() or ""
        if start is None and "SELECTING MAGIC ITEMS" in text:
            start = i
        elif start is not None and not any(h in text for h in CATEGORY_HEADERS) \
                and not ITEM_PATTERN.search(text.replace(" Points", " points")):
            end = i
            break
    if start is None:
        raise SystemExit("Could not find the magic items chapter")
    return start, end or start + 1


def extract_items(pdf_path):
    items = []
    with pdfplumber.open(pdf_path) as pdf:
        start, end = find_section_pages(pdf)
        text = "\n".join(extract_column_text(pdf.pages[i]) for i in range(start, end))

    category = None
    current = None
    for raw_line in text.splitlines():
        line = raw_line.strip()
        if not line:
            continue

        header = line.rstrip(" .")
        if header in CATEGORY_HEADERS:
            category = CATEGORY_HEADERS[header]
            current = None
            continue

        m = ITEM_PATTERN.match(line)
        if m and category:
            current = {
                "name": title_case(m.group(1).strip()),
                "points": m.group(3),
                "category": category,
                "description": "",
            }
            if m.group(2):
                current["common"] = True
            items.append(current)
        elif current is not None:
            # Description lines run until the next item or header
            current["description"] = (current["description"] + " " + line).strip()

    return items


def main():
    import argparse
    parser = argparse.ArgumentParser(description="Extract common magic items from the WAP rulebook PDF")
    parser.add_argument("pdf", help="Path to the rulebook PDF")
    parser.add_argument("--output", default=None, help="Output JSON file path")
    parser.add_argument("--pretty", action="store_true", help="Pretty-print JSON output")
    args = parser.parse_args()

    items = extract_items(args.pdf)
    json_str = json.dumps(items, indent=1 if args.pretty else None, ensure_ascii=False)

    if args.output:
        with open(args.output, "w", encoding="utf-8") as f:
            f.write(json_str)
        from collections import Counter
        counts = Counter(i["category"] for i in items)
        print(f"Extracted {len(items)} items -> {args.output}")
        for cat, n in counts.items():
            print(f"  {cat}: {n}")
    else:
        print(json_str)


if __name__ == "__main__":
    main()
