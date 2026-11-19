# B1110 current rule and forwarding CPU profile

The verified `3e67fd010` image with core SHA-256 beginning `aa15d342`
ran the three unchanged B1109 scripts on the quiet Azure F4as_v7,
Ubuntu 24.04.4 and .NET 11 RC3 on November 19, 2026 UTC. The profiler
sampled all worker threads after at least eight seconds of warm
execution per script. The boundary includes worker framing, fixture
reset, golden checks and prior-result cleanup.

| Samples or nearest runtime frame | Ordinary rules | Captured rules | Expanded forwarding |
| --- | ---: | ---: | ---: |
| All samples | 1,026 | 1,165 | 1,171 |
| Runtime ancestry | 1,017 | 1,158 | 1,118 |
| Unknown leaf | 85 | 95 | 172 |
| Instruction prefix | 133 | 169 | 123 |
| Admission | 125 | 120 | 69 |
| Builtin arithmetic | 100 | 90 | 5 |
| Bound-call entry | 28 | 77 | 0 |
| Executable function creation | 34 | 26 | 0 |
| Split construction | 0 | 0 | 69 |
| Expanded keyword preparation | 0 | 0 | 43 |
| Prepared positional formatting | 0 | 0 | 48 |

Ordinary-rule admission frequently occurs in pending default
construction (28 samples), function creation (28), record creation (20)
and tuple construction (18). Captured rules instead show admissions
from function creation (48), executable value storage (27) and capture
construction (19). Inspect these paths separately. Ordinary compiled
definitions build a pending defaults value and populate the retained
default tuple from executed operands. A redesign can test whether the
final payload can own the preflight and receive values directly.
Captured-rule work storage has a different lifetime. Forwarding still
shows split, keyword snapshot and formatting work after the trim change.

Stack samples can omit inlined work. The counts identify areas to
inspect and do not establish avoidable CPU or a measured speedup.
All complete-image, golden, quiet and worker-join checks passed.
The VM verified the persistent trace before removing guest fixtures.

[Attribution](attribution.json) · [Raw observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) · [Procedures](procedures.tar.xz).
