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
| `extract_pdf.py` | An army book PDF | Unit profiles → `data/factions/<faction>.json` |
| `extract_army_extras.py` | An army book PDF | Army rules, magic items, lores, faction upgrades → `data/factions/<faction>-extras.json` |
| `extract_core_rules.py` | The core rules PDF | Combat tables, weapon/armour profiles, common special rules → `data/core-rules.json` |

All three take the PDF path as the first positional argument and write to `--output`.
Pass `--pretty` for readable JSON. `--help` lists the rest.

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
