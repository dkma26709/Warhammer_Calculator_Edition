"""Export the WAR army data from warhammer-armies-revamped into Warhammer/wwwroot/data/war/.

The data is not committed: this script downloads the upstream repo at the commit in
tools/war-data/pin (or at --ref), runs its export.py and replaces every file of the data folder
but core-rules.json and troop-types.json, which are this repo's own. It records what it exported
in tools/war-data/.exported, which the build checks against the pin, and does nothing when the
data is already at that commit. Local builds, test.yml and the Docker image all run it.

typst 0.15.1 is taken from TYPST, else from PATH, else downloaded (checksum-verified) into
tools/war-data/.cache/.

    python tools/war-data/fetch.py                  # the pin
    python tools/war-data/fetch.py --ref <ref>      # a branch, tag or full commit SHA
    python tools/war-data/fetch.py --out DIR        # into DIR only: no stamp, no up-to-date check
"""
from __future__ import annotations

import argparse
import hashlib
import os
import platform
import re
import shutil
import subprocess
import sys
import tarfile
import tempfile
import urllib.error
import urllib.request
import zipfile
from pathlib import Path

import stub_lfs_images

HERE = Path(__file__).resolve().parent
DATA_DIR = HERE.parent.parent / "Warhammer" / "wwwroot" / "data" / "war"
PIN_FILE = HERE / "pin"
STAMP_FILE = HERE / ".exported"
CACHE_DIR = HERE / ".cache"

UPSTREAM = "Anders1222/warhammer-armies-revamped"
# This repo's own files in the data folder; the export writes every other one.
OWN_FILES = {"core-rules.json", "troop-types.json"}

TYPST_VERSION = "0.15.1"
# (platform.system(), normalised machine) -> release asset and GitHub's sha256 of it.
TYPST_ASSETS = {
    ("Linux", "x86_64"): ("typst-x86_64-unknown-linux-musl.tar.xz",
                          "a6d077d0a95eed5a2eba715b2dae06be954f624ccbf85758a03f389ded33118c"),
    ("Linux", "aarch64"): ("typst-aarch64-unknown-linux-musl.tar.xz",
                           "5aa8d74a3d906e60ea12a66ac2f37f8eef1b14cbad7182a745e393a10c23dcee"),
    ("Windows", "x86_64"): ("typst-x86_64-pc-windows-msvc.zip",
                            "19ce3551153c2fe7ee9fa2f95208310c8f4d3209fedb699e0333faf8913f6736"),
    ("Windows", "aarch64"): ("typst-aarch64-pc-windows-msvc.zip",
                             "4ab28e1b71ec3184d38d580ab797f499b6770d952b6b19167be5cea5c2662e14"),
    ("Darwin", "aarch64"): ("typst-aarch64-apple-darwin.tar.xz",
                            "48f62ed034aa3a7978309579ac6ca00045e2ef0da73114e8af27cfd8e74dc05a"),
    ("Darwin", "x86_64"): ("typst-x86_64-apple-darwin.tar.xz",
                           "7f9fdd9584866245de9a79e0add8f9236fae6f40a8a45e2c4771ccc14db4e0fa"),
}
MACHINES = {"amd64": "x86_64", "x86_64": "x86_64", "arm64": "aarch64", "aarch64": "aarch64"}


class FetchError(Exception):
    pass


def say(message: str) -> None:
    # Flushed, so it stays in order with export.py's output on a pipe.
    print(f"fetch: {message}", flush=True)


def download(url: str, dest: Path) -> None:
    try:
        with urllib.request.urlopen(url, timeout=60) as response, dest.open("wb") as f:
            shutil.copyfileobj(response, f)
    except urllib.error.HTTPError as e:
        raise FetchError(f"downloading {url}: HTTP {e.code} {e.reason}") from e
    except urllib.error.URLError as e:
        raise FetchError(f"downloading {url}: {e.reason}") from e


def read_pin() -> str:
    pin = PIN_FILE.read_text(encoding="utf-8").strip()
    if not re.fullmatch(r"[0-9a-f]{40}", pin):
        raise FetchError(f"{PIN_FILE} must hold one full commit SHA, not {pin!r}")
    return pin


def resolve_ref(ref: str) -> str:
    """The commit a branch, tag or full SHA of the upstream repo names."""
    if re.fullmatch(r"[0-9a-fA-F]{40}", ref):
        return ref.lower()
    git = shutil.which("git")
    if git is None:
        raise FetchError(f"resolving --ref {ref} needs git on PATH; or pass a full commit SHA")
    url = f"https://github.com/{UPSTREAM}.git"
    # No credential prompt: a missing ref or repo must fail, not wait for input.
    result = subprocess.run([git, "ls-remote", url], capture_output=True, text=True,
                            env={**os.environ, "GIT_TERMINAL_PROMPT": "0"})
    if result.returncode != 0:
        lines = result.stderr.strip().splitlines()
        raise FetchError(f"git ls-remote {url}: {lines[-1] if lines else 'failed'}")
    refs = {}
    for line in result.stdout.splitlines():
        sha, _, name = line.partition("\t")
        refs[name] = sha
    for name in (f"refs/heads/{ref}", f"refs/tags/{ref}^{{}}", f"refs/tags/{ref}"):
        if name in refs:
            return refs[name]
    raise FetchError(f"{ref} is not a branch or tag of {UPSTREAM}; "
                     "pass a branch, a tag or a full commit SHA")


def read_stamp() -> tuple[str, str] | None:
    """The (commit, source) the data folder was last exported at, if recorded."""
    if not STAMP_FILE.is_file():
        return None
    commit, _, source = STAMP_FILE.read_text(encoding="utf-8").strip().partition(" ")
    return commit, source


def is_up_to_date(commit: str) -> bool:
    """The data folder holds the export of commit. The export depends on the commit alone, so
    whether it was asked for as the pin or as a ref does not matter."""
    stamp = read_stamp()
    return (stamp is not None and stamp[0] == commit
            and (DATA_DIR / "manifest.json").is_file())


def write_stamp(commit: str, source: str) -> None:
    STAMP_FILE.write_text(f"{commit} {source}\n", encoding="utf-8", newline="\n")


def typst_version(typst: str) -> str:
    try:
        return subprocess.run([typst, "--version"], capture_output=True, text=True).stdout
    except OSError:
        return ""


def is_wanted_typst(typst: str) -> bool:
    return typst_version(typst).split()[:2] == ["typst", TYPST_VERSION]


def download_typst(cache_dir: Path) -> Path:
    """typst TYPST_VERSION for this machine, downloaded into cache_dir on first use."""
    system, machine = platform.system(), platform.machine().lower()
    asset = TYPST_ASSETS.get((system, MACHINES.get(machine, machine)))
    if asset is None:
        raise FetchError(f"no typst download for {system} {machine}: install typst "
                         f"{TYPST_VERSION} and put it on PATH or set TYPST to it")
    name, sha256 = asset
    exe = "typst.exe" if name.endswith(".zip") else "typst"
    folder = cache_dir / f"typst-{TYPST_VERSION}"
    binary = folder / exe
    if binary.is_file():
        return binary

    folder.mkdir(parents=True, exist_ok=True)
    archive = folder / name
    say(f"downloading typst {TYPST_VERSION} ({name})")
    try:
        download(f"https://github.com/typst/typst/releases/download/v{TYPST_VERSION}/{name}",
                 archive)
        if hashlib.sha256(archive.read_bytes()).hexdigest() != sha256:
            raise FetchError(f"{name} does not match its sha256 checksum")
        # Only the binary, from typst-<triple>/ inside the archive.
        member = f"{name.removesuffix('.zip').removesuffix('.tar.xz')}/{exe}"
        partial = folder / f"{exe}.partial"
        try:
            if name.endswith(".zip"):
                with zipfile.ZipFile(archive) as z:
                    partial.write_bytes(z.read(member))
            else:
                with tarfile.open(archive, "r:xz") as t:
                    f = t.extractfile(member)
                    if f is None:
                        raise KeyError(member)
                    partial.write_bytes(f.read())
        except KeyError:
            raise FetchError(f"{name} has no {member}") from None
        if os.name != "nt":
            partial.chmod(0o755)
        # Renamed into place only when complete, so a broken download is never reused.
        os.replace(partial, binary)
    finally:
        archive.unlink(missing_ok=True)
    return binary


def find_typst() -> str:
    typst = os.environ.get("TYPST")
    if typst:
        if not is_wanted_typst(typst):
            print(f"fetch: warning: TYPST={typst} is not typst {TYPST_VERSION}; "
                  "the export may differ", file=sys.stderr, flush=True)
        return typst
    on_path = shutil.which("typst")
    if on_path and is_wanted_typst(on_path):
        return on_path
    return str(download_typst(CACHE_DIR))


def replace_folder(target: Path, exported: Path) -> None:
    """Make target hold the export plus this repo's own files, and nothing else."""
    target.mkdir(parents=True, exist_ok=True)
    # Deepest first, so a folder is empty by the time it is reached.
    for path in sorted(target.rglob("*"), reverse=True):
        if path.is_dir():
            if not any(path.iterdir()):
                path.rmdir()
        elif path.parent != target or path.name not in OWN_FILES:
            path.unlink()
    shutil.copytree(exported, target, dirs_exist_ok=True)


def export(commit: str, target: Path, typst: str) -> None:
    # A temporary folder outside any git repository, removed even on failure.
    work = Path(tempfile.mkdtemp(prefix="war-data-"))
    try:
        say(f"downloading {UPSTREAM} at {commit[:7]}")
        tarball = work / "source.tar.gz"
        download(f"https://codeload.github.com/{UPSTREAM}/tar.gz/{commit}", tarball)
        unpacked = work / "source"
        with tarfile.open(tarball, "r:gz") as tar:
            if hasattr(tarfile, "data_filter"):
                tar.extractall(unpacked, filter="data")
            else:
                tar.extractall(unpacked)
        tops = list(unpacked.iterdir())
        if len(tops) != 1 or not tops[0].is_dir():
            raise FetchError("the upstream tarball does not hold exactly one top-level folder")
        source = tops[0]

        stub_lfs_images.main(source)
        say("running export.py")
        out = work / "out"
        env = {**os.environ, "TYPST": typst, "TYPST_IGNORE_SYSTEM_FONTS": "true",
               # export.py records `git rev-parse HEAD`; never let it read an enclosing repo.
               "GIT_DIR": str(work / "no-git")}
        result = subprocess.run([sys.executable, "export.py", "--out", str(out)],
                                cwd=source, env=env)
        if result.returncode != 0:
            raise FetchError(f"export.py exited with code {result.returncode}")

        replace_folder(target, out)
        say(f"exported {commit[:7]} into {target}")
    finally:
        shutil.rmtree(work, ignore_errors=True)


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--ref", help="export this warhammer-armies-revamped branch, tag or full "
                                  "commit SHA instead of the pin")
    ap.add_argument("--force", action="store_true",
                    help="export even when the data is already at that commit")
    ap.add_argument("--out", type=Path, metavar="DIR",
                    help="export into DIR only, leaving the app's data folder and the stamp alone")
    args = ap.parse_args()

    # The export clears the folder it writes, so --out only takes a new or empty one.
    if args.out is not None and args.out.exists() and any(args.out.iterdir()):
        raise FetchError(f"--out {args.out} is not empty; name a new or empty folder")
    commit = resolve_ref(args.ref) if args.ref else read_pin()
    source = f"ref:{args.ref}" if args.ref else "pin"
    if args.out is None and not args.force and is_up_to_date(commit):
        # Same data; only the label of what was asked for may change (say, the pin after --ref).
        write_stamp(commit, source)
        say(f"WAR data up to date at {commit[:7]}")
        return

    export(commit, args.out or DATA_DIR, find_typst())
    if args.out is None:
        # Last, so a failed export never leaves a stamp claiming it succeeded.
        write_stamp(commit, source)


if __name__ == "__main__":
    try:
        main()
    except (FetchError, OSError, tarfile.TarError, zipfile.BadZipFile) as e:
        sys.exit(f"fetch: {e}")
