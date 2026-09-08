import json
data = json.loads(open("/input.json").read())
data.update({"status": "done"})
data["items"].append("gamma")
__lython_file = open("/output.json", "w")
__lython_file.write(json.dumps(data))
__lython_file.close()
