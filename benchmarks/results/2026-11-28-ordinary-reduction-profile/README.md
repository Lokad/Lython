# B1939 Attribute ordinary generator reduction

The November 28, 2026 Ubuntu WSL captures use the exact Lython
`e036d6597` worker image and .NET 11 RC3. All 32 image files match
B1937, where ordinary helper and generator reduction uses
1.567–1.592× CPython process CPU. The file-fed quantity map from the
same CSV uses 0.346–0.352×. Each capture runs the whole script on
one logical CPU after four seconds of verified warmup, then samples
managed stacks for six seconds. All 203 sampled batches verify and
all workers and trace processes join.

| Workload | Verified invocations | Generator advancement¹ | Generator construction¹ | CSV record¹ | Mediated text¹ |
| --- | ---: | ---: | ---: | ---: | ---: |
| Ordinary reduction, `RunAsync` | 4,032 | 32.1% | 9.9% | 10.5% | 10.1% |
| Ordinary reduction, `Run` | 1,472 | 24.5% | 12.4% | 12.9% | 12.5% |
| File-fed quantity map, `RunAsync` | 7,488 | 0.0% | 0.0% | 43.2% | 51.5% |

¹ Inclusive share of resolved Lython stack time after filtering known
waits and unclassified native leaves. Regions include guest work and
overlap. Each row has its own denominator. The captures locate work
for inspection and do not measure an attainable speedup.

The nearest methods in async reduction include executable instruction
dispatch at 9.1%, asynchronous loop advancement at 7.2%, the memory
governor's reserve method at 5.3% and the generator construction state
machine at 2.1%. The corresponding sync generator construction share
is 2.3%. The file-fed control concentrates on CSV record emission
and reading and has no generator construction samples.

The ordinary callback constructs and drains a generator expression
for every row. The existing executable path first publishes a Python
generator, then the builtin sum opens an iteration cursor and
selects the same-command buffered generator drain. Test whether the
already-evaluated builtin target and adjacent generator expression
permit handing that admitted frame directly to the same reduction
loop. Keep the original outer iteration, closure and suspension
behavior. The archived capture cannot establish which eligible stage
accounts for the entire gap.

[Quiet timing](../2026-11-28-current-cpython-shipment/README.md) ·
[Managed attribution](managed-attribution.json) · [Image identity](identity.json) ·
[Capture summary](run-summary.json) · [Raw captures and procedures](observations.tar.xz).
