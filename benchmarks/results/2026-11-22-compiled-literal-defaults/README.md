# B1324 Compiler-owned literal positional defaults

One immutable tuple belongs to the compiled declaration when all its
positional defaults are scalar literals. Repeated definitions reuse the
tuple and its values. Assigning `__defaults__` still changes one function.
Mutable keyword dictionaries and evaluated positional defaults keep their
own execution storage. Positional-only construction emits one constant
load. Mixed declarations build only their keyword defaults at execution.

Compared `8c81fb8d2` with `072ab6065` on November 22, 2026 UTC on the
quiet Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3. Two independent
processes per revision warmed for 12 seconds each and supplied seven
rotated batches. Ranges cover all process-median pairings. Negative
changes favor the candidate. Warm elapsed includes execution, output
verification and result disposal. Time to result adds compilation and
code-owner disposal. CPU includes the wider request. The fixtures use no
file I/O. Startup and final worker teardown are outside both clocks.

| Complete script | Boundary | Elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | --- | ---: | ---: | ---: |
| Literal positional and evaluated keyword defaults | Warm | −15.62 to −8.86% | −15.48 to −9.71% | 5.61 / 2.27% |
| Literal positional defaults only | Warm | −16.41 to −12.11% | −16.82 to −11.31% | 0.33 / 4.80% |
| Dynamic positional defaults control | Warm | +1.08 to +5.36% | +0.81 to +5.37% | 3.13 / 1.06% |
| Fully supplied ordinary helper control | Warm | −0.46 to +1.80% | −0.58 to +2.40% | 0.49 / 1.78% |
| Literal positional and evaluated keyword defaults | Compile to result | −14.77 to −1.00% | −14.75 to −2.15% | 2.53 / 13.29% |

Local managed allocation falls 18.09% in the mixed factory and 15.51%
in the positional-only factory. Each uses 491,520 fewer accounted bytes
at peak. Both controls have identical allocation and peaks. Every tested
budget returns to baseline. Time to result is favorable with a 13.29%
candidate spread. Keep the source private until a refinement removes the
changes shared with dynamic construction and rechecks that control.

Windows .NET 11 and Ubuntu .NET 10 each pass 631 white-box and 2,363
public focused Release tests, plus all four default-accounting stress
cases at their original 5,000-definition scale and limits. CPython confirms
shared literal tuple identity, per-function reassignment and private
keyword dictionaries. The public test also evaluates a keyword default
through real async host I/O. The initial public results retain two failures
from fixtures that relied on allocating separate tuples for identical
literals. Those fixtures now create distinct evaluated defaults and still
require terminal denial. The first Ubuntu wrapper completed the tests and
failed to find its summary command. The test logs verify all three results.

Both complete worker images pass CPython goldens. All 140 timed batches
verify. Both quiet checks pass, workers join, fixtures are removed and the
persistent archive is verified under the lease.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Source and checks](checks-and-procedures.tar.xz) ·
[Reference core](../2026-11-22-sparse-lexical-preparation/candidate-core.dll.gz).
