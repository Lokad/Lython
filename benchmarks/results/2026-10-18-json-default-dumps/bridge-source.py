import json
data = json.loads(open("input.json").read())
data["items"] = [data["items"][0], data["items"][2]]
__lython_file = open("output.json", "w")
__lython_file.write(json.dumps(data))
__lython_file.close()
