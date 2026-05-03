import json
data = json.loads(read_text("/input.json"))
data.update({"status": "done"})
data["items"].append("gamma")
write_text("/output.json", json.dumps(data))
