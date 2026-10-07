"""Regenerate every manual from a clean database, then build the .docx/.pdf.

Prerequisites: the local app is running with a seeded snapshot (see README.md in this folder).
    python tools/manuals/run_all.py [spec-substring ...]
"""
import os
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
SCRATCH = os.environ.get("LOTV_MANUALS_SCRATCH", str(Path(os.environ.get("TEMP", "/tmp")) / "lotv-manuals"))
SPECS = ["m01_hub", "m02_pray", "m03_pack", "m04_admin", "m05_cases", "m06_board", "m07_pipeline", "m08_hub"]


def main():
    want = sys.argv[1:]
    for s in SPECS:
        if want and not any(w in s for w in want):
            continue
        print(f"=== {s}: restoring clean data", flush=True)
        subprocess.run(["powershell", "-NoProfile", "-File", str(HERE / "fast_reset.ps1"), "-Scratch", SCRATCH], check=True)
        subprocess.run([sys.executable, str(HERE / "specs" / f"{s}.py")], check=True)
    subprocess.run([sys.executable, str(HERE / "build.py")], check=True)


if __name__ == "__main__":
    main()
