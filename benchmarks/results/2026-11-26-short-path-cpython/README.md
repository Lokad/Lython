# B1750 Protected search, bound shipment and regex selectors against CPython

Lython `0a0e32c16` and stock CPython 3.13.16 ran on the quiet Azure F4as_v7
on November 26, 2026 UTC with Ubuntu 24.04.4 and .NET 11 RC3. Two workers
per engine and phase contribute seven rotated batches each. The ratios
divide Lython by CPython and span all pairings of process medians. Values
above 1× mean Lython took longer.

| Workload | Input or source | Phase | Lython | CPython | Elapsed ratio |
| --- | ---: | --- | ---: | ---: | ---: |
| Bound `starmap` shipment | 2,048 rows, 27,350 B | Warm `RunAsync` | 2.52384–2.56812 ms | 1.66237–1.71676 ms | 1.470–1.545× |
| Protected file search | 1,025 lines of a 42,010 B file | Warm | 0.18709–0.18918 ms | 0.13516–0.13913 ms | 1.345–1.400× |
| Protected file search | 319 B of source | Compile-to-result | 0.22328–0.22538 ms | 0.21057–0.21383 ms | 1.044–1.070× |
| Generated literal regex selectors | 48 selectors, 197,820 B of source | Compilation only | 0.79275–0.80053 ms | 0.56563–0.56772 ms | 1.396–1.415× |
| Generated literal regex selectors | 48 selectors, 197,820 B of source | Compile-to-result | 1.06505–1.07562 ms | 1.27062–1.27378 ms | 0.836–0.847× |

Warm execution includes applicable mediated RAM-backed reads and writes,
output and file checks, and previous-result disposal. Compile-to-result
adds compilation and code-owner disposal. Compilation only includes the
engine's normal compile-time work and disposal. Lython evaluates constant
regex matches for diagnostics at that boundary. CPython compiles and
matches the regex when it executes the script. The combined phase includes
both engines' work through a verified result. The async row uses the same
synchronous guest script through Lython's `RunAsync` and CPython's native
file APIs. Lython accounts for a shared memory allowance throughout.
Process startup is outside the clocks. Wider process CPU includes fixture
reset and framing.

Protected search is close through result and still slower warm. Its wider
CPU ratios are 0.977–0.997× through result and 1.148–1.220× warm. Bound
shipment uses 1.490–1.580× CPython's process CPU. No inter-revision speedup is
derived from this comparison. [Earlier protected search](../2026-11-26-file-processing-cpython/README.md) ·
[Direct affix comparison](../2026-11-26-direct-affix-return/README.md) ·
[Direct zip operand comparison](../2026-11-26-zip-starmap-operands/README.md).

The initial launch stopped before fixture setup because the collection
manifest omitted the image path. A manifest preflight identified a missing
auxiliary inventory as well. The retry verified the full original staged
image, corrected manifest and unchanged workload catalog. All 140 timed
batches and the independent output and file goldens passed. Both quiet
checks passed, workers joined, and the archive verified under the lease.

[Summary](summary.json) · [Identity](identity.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures](procedures.tar.xz).
