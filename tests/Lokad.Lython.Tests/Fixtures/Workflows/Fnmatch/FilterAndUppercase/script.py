import fnmatch
import os
from pathlib import Path

Path("/out").mkdir()
for name in fnmatch.filter(os.listdir("/src"), "*.txt"):
    text = Path("/src", name).read_text().upper()
    Path("/out", name).write_text(text)
