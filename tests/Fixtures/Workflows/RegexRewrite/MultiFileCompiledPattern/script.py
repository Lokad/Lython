from pathlib import Path
import re

WORD_RE = re.compile(r"\balpha\b", re.IGNORECASE)

out_dir = Path("/out")
out_dir.mkdir()

for path in sorted(Path("/docs").glob("*.txt")):
    updated = WORD_RE.sub(lambda match: "omega" if match.group(0).islower() else "OMEGA", path.read_text())
    __lython_file = open((out_dir / path.name).as_posix(), "w")
    __lython_file.write(updated)
    __lython_file.close()
