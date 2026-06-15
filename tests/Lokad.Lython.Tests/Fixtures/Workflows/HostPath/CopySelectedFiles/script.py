import os
from pathlib import Path

Path("/out").mkdir()
for name in os.listdir("/src"):
    if name != "skip.txt":
        text = Path("/src", name).read_text()
        Path("/out", name).write_text(text)
