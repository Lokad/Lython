"""Create the five trusted diagnostic jobs used by this improvement round.

Usage: python create-diagnostic-catalog.py quick-catalog.json diagnostics.json
Goldens are written explicitly from the fixture pattern, not from execution.
This catalog is for --compare micro, not milestone qualification.
"""
import hashlib
import json
import pathlib
import sys

original = json.loads(pathlib.Path(sys.argv[1]).read_text(encoding="utf-8"))
pipeline = next(c for c in original["cases"] if c["id"] == "strings.pipeline-ascii.medium")
text = "abZ!::tail/" * 1024
replaced = "abZ!/tail/" * 1024
printed = "abZ!|tail|" * 1024
assert pipeline["expectedOutput"] == printed + "\n"
cases = [pipeline]


def add(name, source, expected, fixture):
    row = dict(id="diagnostic.ascii." + name, family="diagnostic", category="strings", scale="medium", size=1024,
               source=source, fixtureJson=json.dumps(dict(size=1024, text=fixture), separators=(",", ":")), expectedOutput=expected)
    for key in ("source", "fixture", "expectedOutput"):
        value = row["fixtureJson" if key == "fixture" else key]
        row[key + "Sha256"] = hashlib.sha256(value.encode("utf-8")).hexdigest()
    cases.append(row)


add("pipeline-length", "TEXT = " + repr(text) + "\nprint(len('|'.join(TEXT.replace('::', '/').split('/'))))\n", "10240\n", text)
add("replace-length", "TEXT = " + repr(text) + "\nprint(len(TEXT.replace('::', '/')))\n", "10240\n", text)
add("split-count", "TEXT = " + repr(replaced) + "\nprint(len(TEXT.split('/')))\n", "2049\n", replaced)
add("output-only", "TEXT = " + repr(printed) + "\nprint(TEXT)\n", printed + "\n", printed)
original["cases"] = cases
with pathlib.Path(sys.argv[2]).open("w", encoding="utf-8", newline="\n") as output:
    output.write(json.dumps(original, separators=(",", ":")) + "\n")
