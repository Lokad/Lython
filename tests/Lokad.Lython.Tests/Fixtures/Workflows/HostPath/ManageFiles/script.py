import os
import shutil
from pathlib import Path

Path("/work").mkdir()
log = os.getcwd() + "\n"
shutil.copyfile("/seed.txt", "/work/copy.txt")
shutil.move("/work/copy.txt", "/work/moved.txt")
if os.path.exists("/work/moved.txt") == True:
    log = log + os.path.basename(os.path.dirname("/work/moved.txt"))
Path("/work/log.txt").write_text(log)
os.remove("/empty.txt")
