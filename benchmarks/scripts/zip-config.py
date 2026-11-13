import json
import zipfile

with zipfile.ZipFile("bundle.zip") as archive:
    names = archive.namelist()
    config = json.loads(archive.read("config.json").decode("utf-8"))

print(len(names), config["version"], config["name"])
