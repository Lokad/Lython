# B1559 attribute CSV meter execution costs

The explicit-float meter, typed Unicode meter and typed meter that observes
every site string each process 4,096 records. The image is `83ae1e2fa`,
with runtime and tests matching shipped `8c88e30d8`. Each script passes
its CPython golden check. The three scripts retain their original inputs
and outputs.

| Nearest resolved Lython method or group | Explicit float | Typed Unicode | Site observer |
| --- | ---: | ---: | ---: |
| Executable instruction dispatch | 306 | 241 | 262 |
| CSV field and record handling | 195 | 212 | 205 |
| Admission or retirement | 82 | 76 | 68 |
| Sequence reads and scalar access | 60 | 53 | 54 |
| Tuple construction | 30 | 20 | 21 |
| Float parsing and Unicode digit lookup | 0 | 235 | 168 |
| Total with a resolved runtime ancestor | 1,179 | 1,210 | 1,246 |

The last named group includes 119, 82 and 34 nearest typed-meter samples
in the general ASCII conversion, normalized float parser and decimal
digit lookup respectively. The explicit meter has other float costs,
including 40 nearest samples in the packed UTF-8 float call. The typed
meters see wide or normalizing inputs that the short ASCII decimal
parser cannot consume. Even though tuple construction accounts for a
large share of allocation, these samples support addressing the
normalized float pipeline first for CPU. A sample count does not
establish the share removable by any particular change.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November
24, 2026 UTC. The worker warmed for eight guest seconds before each
six-second CPU window at 199 Hz. The active threads produced 1,240,
1,266 and 1,292 samples, including 114, 108 and 102 unmapped leaves.
The workers completed 2,048, 1,792 and 1,408 invocations in those
windows. The CPU sampling covers whole worker requests, including reset
and validation. These are attribution traces. The worker counts,
source differences and resolved samples do not establish elapsed ratios.

[Profile counts and methods](attribution.json) · [Image identity](identity.json) ·
[Golden checks](verification.json) · [Raw traces](observations.tar.xz) ·
[Procedures](procedures.tar.xz) · [Terminal receipt](terminal-status.json) ·
[Earlier allocation attribution](../2026-11-24-combined-csv-cpython/README.md).
