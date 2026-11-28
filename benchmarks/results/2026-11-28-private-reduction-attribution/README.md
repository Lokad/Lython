# B1956 Attribute the ordinary and explicit reduction paths

Both Ubuntu WSL captures use the exact B1954 Lython image at `2806d7d75`
on November 28, 2026 UTC. Both scripts run through `RunAsync` and read
the same 2,048 CSV records. The existing helper either consumes a
two-key generator expression or visits the same keys in an explicit
loop. The complete scripts produce the same output. B1954 measured
warm process CPU at 1.427–1.475× CPython for the generator and
0.705–0.720× for the explicit loop. [B1954](../2026-11-28-combined-loop-cpython/README.md).

| Workload | Verified sampled invocations | Generator construction, inclusive | Generator advancement, inclusive | Native instruction loop, nearest |
| --- | ---: | ---: | ---: | ---: |
| Ordinary generator reduction | 1,344 | 10.8% | 26.3% | Outside the top 35 |
| Explicit tuple loop | 4,416 | 0.0% | 0.0% | 42.7% |

Four seconds of verified warmup precede each six-second EventPipe
capture on one logical CPU. Percentages describe resolved Lython
managed-stack time after filtering known waits and unclassified native
leaves. Inclusive regions can overlap. Each script has its own
denominator. The captures attribute work on Lython's side of the
comparison. All 90 sampled batches verify, and both workers and trace
processes join.

The ordinary helper's generated code has a capture for its generator
and is ineligible for the native prefix. The adjacent generator
construction and invocation already carry a prepared private operand.
The explicit helper has eligible native code and enters its iteration
from the same executable operand representation consolidated in B1953.
The generator's code is a normal executable loop with a `Yield` and
already has a prepared record-cache range.

The next source gate is a borrowed reduction through the existing
executable operands and accumulator. It must account for both the
helper's activation and the generator's activation, keep exact callable
and iterator identity, and promote before the first operation that
needs an observable frame. The layout and traces establish where to work.
They give no isolated estimate of the resulting speedup.

The follow-up regression observes the live `.0` from inside a private
reduction and advances it in a callback. The same iterator remains visible
on subsequent pulls and the skipped item stays out of the sum. These
assertions pass on CPython 3.13.16 and the current Lython development
targets. [Regression](../../../tests/Lokad.Lython.PublicApi.Tests/NativeGeneratorSumTests.cs).

[Attribution](managed-attribution.json) · [Layouts](layout.json) ·
[Run summary](run-summary.json) · [Raw traces and procedures](observations.tar.xz).
