"""Re-classify already-extracted unit options against the current parser.

Every option keeps the army book line it came from in its `raw` field, so options
extracted by an older (or buggier) version of `classify_option` can be repaired without
the source PDFs: feed each stored `raw` back through the parser and keep the result.

By default only magic item and magic standard allowances are rewritten — the options a
misparse turns into a meaningless "+50 points" checkbox instead of a points budget.
Pass --all-types to rewrite every option whose classification has changed.

    python reclassify_options.py ../../Warhammer/wwwroot/data --dry-run

Option indices are preserved: an existing option is rewritten in place and anything new is
appended, because saved army lists address a unit's options by index. That guarantee is this
script's reason to exist and does not extend to `extract_pdf.py` — a full re-extraction rebuilds
each file from the PDF and may renumber a unit's options for any number of reasons, so saved
lists have to be treated as stale after one either way.
"""
import argparse
import json
import re
from pathlib import Path

from extract_pdf import classify_option

# Types this script repairs unless --all-types is given.
ALLOWANCE_TYPES = {"magic_items", "magic_standard"}

# A shared allowance emits a companion faction_upgrade option carrying the same `raw`
# text as the allowance itself. Re-classifying one of those would turn it back into the
# allowance and produce a duplicate, so companions are left alone.
COMPANION_TYPE = "faction_upgrade"


def read_json(path):
    """Load a JSON file, remembering its newline style so it can be written back as found."""
    raw_bytes = path.read_bytes()
    newline = "\r\n" if b"\r\n" in raw_bytes else "\n"
    return json.loads(raw_bytes.decode("utf-8")), newline


def write_json(path, data, newline):
    with open(path, "w", encoding="utf-8", newline=newline) as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
        f.write("\n")


def reclassify(option, all_types):
    """Return a corrected option, or None when the stored one already agrees."""
    raw = option.get("raw")
    if not raw or option.get("type") == COMPANION_TYPE:
        return None

    fresh = classify_option(raw)
    if fresh.get("type") == option.get("type"):
        return None
    if not all_types and fresh.get("type") not in ALLOWANCE_TYPES:
        return None
    return fresh


def companion_for(option):
    """Pull the faction upgrade family off an allowance into its own option."""
    category = option.pop("factionUpgrade", None)
    if not category:
        return None
    return {"type": COMPANION_TYPE, "factionUpgrade": category, "raw": option.get("raw", "")}


def has_companion(options, companion):
    return any(
        o.get("type") == COMPANION_TYPE and o.get("factionUpgrade") == companion["factionUpgrade"]
        for o in options
    )


def dedupe_new_companions(companions):
    """One companion per upgrade family, over the batch about to be appended.

    Scoped to the new batch and never to the unit's existing options: dropping an option
    mid-list shifts every index after it, which is the very thing the append-at-the-end rule in
    process_unit exists to avoid. A legacy duplicate already in the file is left alone —
    repairing that is a migration of its own, not something this pass should do silently.
    """
    seen = set()
    kept = []
    for companion in companions:
        family = companion.get("factionUpgrade")
        if family in seen:
            continue
        seen.add(family)
        kept.append(companion)
    return kept


def process_unit(unit, all_types, changes):
    """Rewrite this unit's options in place. Returns the number of options changed."""
    options = unit.get("options") or []
    companions = []
    changed_before = len(changes)

    for i, option in enumerate(options):
        for sub_index, sub in enumerate(option.get("subOptions") or []):
            fresh_sub = reclassify(sub, all_types)
            if fresh_sub:
                # A sub-option's own family would have nowhere sensible to go; the books
                # only ever attach one to a primary line, so drop it rather than guess.
                fresh_sub.pop("factionUpgrade", None)
                changes.append((unit["name"], sub.get("type"), fresh_sub, sub.get("raw")))
                option["subOptions"][sub_index] = fresh_sub

        fresh = reclassify(option, all_types)
        if not fresh:
            continue

        companion = companion_for(fresh)
        # Appended at the end rather than next to the allowance: options are addressed by
        # index in saved army lists, and inserting mid-list would repoint every selection
        # after it at the wrong option.
        if companion and not has_companion(options + companions, companion):
            companions.append(companion)

        changes.append((unit["name"], option.get("type"), fresh, option.get("raw")))
        options[i] = fresh

    if len(changes) == changed_before:
        return 0

    options.extend(dedupe_new_companions(companions))
    unit["options"] = options
    return len(changes) - changed_before


def main():
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("data_dir", help="Path to wwwroot/data")
    parser.add_argument("--dry-run", action="store_true", help="Report changes without writing")
    parser.add_argument("--all-types", action="store_true",
                        help="Rewrite every changed option, not just allowances")
    args = parser.parse_args()

    total = 0
    for path in sorted(Path(args.data_dir).glob("*/factions/*.json")):
        if path.name.endswith("-extras.json"):
            continue

        data, newline = read_json(path)
        units = data["units"] if isinstance(data, dict) else data

        changes = []
        for unit in units:
            process_unit(unit, args.all_types, changes)

        if not changes:
            continue

        total += len(changes)
        print(f"{path.parent.parent.name}/{path.name}: {len(changes)} option(s)")
        for unit_name, was, now, raw in changes:
            detail = f"pointsLimit={now.get('pointsLimit')}"
            if now.get("appliesTo"):
                detail += f", appliesTo={now['appliesTo']!r}"
            print(f"    {unit_name}: {was} -> {now['type']} ({detail})")
            print(f"        {raw}")

        if not args.dry_run:
            write_json(path, data, newline)

    print(f"\n{'Would rewrite' if args.dry_run else 'Rewrote'} {total} option(s)")


if __name__ == "__main__":
    main()
