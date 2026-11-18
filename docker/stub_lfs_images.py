"""Replace Git LFS pointer files with a 1x1 PNG, so export.py runs on a checkout without LFS.

Docker's `ADD <git url>` checks warhammer-armies-revamped out without its Git LFS objects, so
the army books' cover art arrives as pointer text, which typst refuses to decode as an image.
The export reads each record's metadata, never the pictures, so a placeholder image yields
the same files as a full checkout, without fetching ~50 MB of covers on every build.

    python stub_lfs_images.py <checkout>
"""
from __future__ import annotations

import struct
import sys
import zlib
from pathlib import Path

POINTER = b"version https://git-lfs.github.com/spec/v1"


def png_1x1() -> bytes:
    def chunk(kind: bytes, data: bytes) -> bytes:
        body = kind + data
        return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body))

    header = struct.pack(">IIBBBBB", 1, 1, 8, 0, 0, 0, 0)  # 1x1, 8-bit greyscale
    pixels = zlib.compress(b"\x00\x00")                    # filter byte, one pixel
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header) + chunk(b"IDAT", pixels)
            + chunk(b"IEND", b""))


def main(root: Path) -> None:
    png = png_1x1()
    replaced = 0
    for path in sorted(root.rglob("*")):
        if ".git" in path.parts or not path.is_file():
            continue
        with path.open("rb") as f:
            if f.read(len(POINTER)) != POINTER:
                continue
        if path.suffix.lower() != ".png":
            sys.exit(f"stub: {path} is a Git LFS pointer but not a PNG; "
                     "add a placeholder for its format")
        path.write_bytes(png)
        replaced += 1
    print(f"stub: replaced {replaced} Git LFS pointer(s) with a placeholder PNG")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    main(Path(sys.argv[1]))
