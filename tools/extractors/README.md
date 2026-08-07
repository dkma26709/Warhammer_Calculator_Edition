# WAP PDF Extractors

Python scripts that turn Warhammer Armies Project (WAP) 9th Edition PDFs into the JSON
consumed by the app at `Warhammer/wwwroot/data/`.

The PDFs are not in this repo — they live outside it and are passed in by path.

## Setup

```bash
pip install -r requirements.txt
```

## Scripts

| Script | Input | Output |
|---|---|---|
| `extract_pdf.py` | An army book PDF | Unit profiles → `data/{system}/factions/<faction>.json` |
| `extract_army_extras.py` | An army book PDF | Army rules, magic items, lores, faction upgrades → `data/{system}/factions/<faction>-extras.json` |
| `extract_core_rules.py` | The core rules PDF | Combat tables, weapon/armour profiles, common special rules → `data/{system}/core-rules.json` |
| `extract_common_magic_items.py` | The core rules PDF | Common magic items → `data/{system}/common-magic-items.json` |
| `reclassify_options.py` | Already-extracted faction JSON | The same files, with mis-parsed options repaired in place |

The four extractors take the PDF path as the first positional argument and write to
`--output`. Pass `--pretty` for readable JSON. `--help` lists the rest.

`reclassify_options.py` needs no PDF: every option keeps the army book line it came from
in its `raw` field, so improvements to `classify_option` can be applied to data extracted
by an older version of the parser. Run it with `--dry-run` first — it prints every option
it would rewrite.

```bash
python reclassify_options.py ../../Warhammer/wwwroot/data --dry-run
```

## Usage

```bash
python extract_pdf.py "<pdfs>/Warhammer - Lizardmen.pdf" --faction Lizardmen \
    --output ../../Warhammer/wwwroot/data/factions/lizardmen.json --pretty

python extract_army_extras.py "<pdfs>/Warhammer - Lizardmen.pdf" --faction Lizardmen \
    --output ../../Warhammer/wwwroot/data/factions/lizardmen-extras.json --pretty
```

`extract_army_extras.py` also accepts `--all --dir <pdfs>` to batch a whole directory.

## Notes

The WAP PDFs use a two-column layout. Tables span the full page width, body text does
not — extraction uses `pdfplumber.extract_tables()` for tables and `page.crop()` for
column-aware text. Layout changes between book versions are the usual cause of
column-shifted stat lines, so spot-check M/WS/BS/S/T/W/I/A/Ld after re-extracting.

v3 army books replace the LORDS/HEROES sections with a single CHARACTERS section
whose entries can hold several alternative characters (e.g. "Saurus Leaders" =
Oldblood + Scar-Veteran, each with its own points). `extract_pdf.py` splits such
entries into one unit per costed profile and emits them with category `Characters`.
