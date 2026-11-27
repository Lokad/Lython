# B1861 active CPU in compiled-pattern log search

The profile reuses the exact `7305efabf` image, 3,072-line fixture and
compiled-pattern script from B1860. It uses the signed PythonRe 0.5.0
adapter and comparison host `05c1059de` on the Azure F4as_v7 running
Ubuntu 24.04.4 and .NET 11 RC3. Local and remote smoke checks verify
the complete frozen image and compare output and files with CPython.
[B1860](../2026-11-27-direct-regex-cpython/README.md).

After eight seconds of warmup, one 199 Hz CPU-clock capture covers 5.99
seconds. The worker verifies 2,048 invocations. The sampled execution
thread has 1,222 leaf samples.

| Sampled leaf | Count |
| --- | ---: |
| Lython core | 538 |
| Utf8Regex or PythonRe backend | 290 |
| Optional adapter | 25 |
| CLR lock or monitor | 175 |
| Unmapped | 105 |
| Other resolved | 89 |

Leaf categories use the declaring assembly, with the resolved other
category separating CLR lock and monitor methods. `ExecutePrepared`
is the nearest core method for 591 samples, including time in callees.
Its own leaf has 69 samples. The backend slice runner and scalar
one-shot entry have 75 and 51 leaf samples. Core instruction dispatch
has 107. Of the 49 samples with `MemoryGovernor.Reserve` as the nearest
core method, 19 come through UTF-8 copying, 14 through the regex call
and 13 through file-window value creation. These are sample counts
from one command. They do not establish the speedup available from
changing any particular stage.

Source inspection finds that single matching still creates a prepared
subject, calls its match method once and disposes it. This is a candidate
for a one-shot provider operation. Cursor iteration still needs a durable
subject and the test must preserve authority over its lifetime. Validate
the provider contract and compare complete scripts before drawing a
performance conclusion.

Both quiet gates pass. Workers join, the guest fixtures are removed and
the persistent archive verifies under the lease.
[Attribution](attribution.json) · [Image identity](identity.json) ·
[Raw capture](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures](procedures.tar.xz).
