import json


def read_json(path):
    with open(path, encoding="utf-8") as source:
        return json.load(source)


def write_json(path, document):
    with open(path, "w", encoding="utf-8", newline="") as target:
        json.dump(document, target, ensure_ascii=False, indent=2, sort_keys=True)
        target.write("\n")


def normalized_labels(values):
    return sorted({value.strip().lower() for value in values if value.strip()})


def parse_requirements(path):
    records = {}
    with open(path, encoding="utf-8") as source:
        for raw in source:
            line = raw.partition("#")[0].strip()
            if not line:
                continue
            name, separator, version = line.partition("==")
            name = name.strip().lower().replace("_", "-")
            records[name] = {"name": name, "version": version.strip() if separator else None}
    return [records[name] for name in sorted(records)]


def render_targets(config):
    labels = config.get("defaults", {}).get("labels", [])
    return [
        {
            "name": item["name"].strip().lower(),
            "path": item["path"].strip(),
            "labels": normalized_labels(labels + item.get("labels", [])),
        }
        for item in config.get("targets", [])
        if item.get("enabled", True)
    ]


def main():
    config = read_json("config.json")
    dependencies = parse_requirements("requirements.txt")
    targets = render_targets(config)
    config["project"] = config["project"].strip()
    config["targets"] = targets
    config["summary"] = {"targets": len(targets), "dependencies": len(dependencies)}
    manifest = {"project": config["project"], "targets": targets, "dependencies": dependencies}
    write_json("config.json", config)
    write_json("manifest.json", manifest)
    print(f"{len(targets)} targets / {len(dependencies)} dependencies")


main()
