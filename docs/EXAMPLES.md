# File-processing example

This script reads an inventory, normalizes names and writes TSV/JSON outputs.
Its `re` use requires the engine's explicit [regex provider](REGEX.md); file
operations require a supplied [host](RUNTIME.md#host-authority-and-io). It is
checked verbatim by the public documentation-example test.

## Inventory report

```python
import csv
import re
from json import dumps

def clean_name(text, *, pattern=r"\s+"):
    return re.sub(pattern=pattern, repl=" ", string=text.strip())

selected = []
with open("inventory.tsv") as handle:
    # Materialize this small input before closing its single-pass reader.
    rows = list(csv.reader(handle, delimiter="\t"))
for sku, name, qty in rows[1:]:
    if (count := int(qty)) > 0:
        selected.append({"sku": sku, "name": clean_name(name), "qty": count})
selected = sorted(selected, key=lambda item: item["sku"])

writer = csv.writer(delimiter="\t")
writer.writerow(["sku", "name", "qty"])
writer.writerows([[item["sku"], item["name"], item["qty"]] for item in selected])

with open("available.tsv", "w") as handle:
    handle.write(writer.getvalue())

with open("available.json", "w") as handle:
    handle.write(dumps(obj=selected))
```

The destination-free CSV writer buffers output and exposes `getvalue()`. A writer
given a text handle publishes rows through that handle instead. This example
retains its small input and result; long inputs need [bounded host-driven batches](MEMORY.md#streaming-and-approximation).

For example, with the optional adapter and an application-provided host:

```csharp
var engine = new LythonEngine(Utf8RegexProvider.Instance);
using var compiled = engine.Compile(source, sharedBudget);
using var result = compiled.Run(host);
```

The pinned adapter cannot support cancellable backend work. Supply a cooperating
provider for cancellable regex execution; see [adapter capabilities](REGEX.md#optional-utf8regex-adapter).
