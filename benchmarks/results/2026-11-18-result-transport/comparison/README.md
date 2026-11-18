# Result transport withdrawn after whole-script comparison

R531/B961 on 2026-11-18 UTC compared the qualified `925397f65` worker
(`2c6b304b`) with the private `76555c49` prototype. The 32-file images differ
only in the core assembly. Both passed the 13-case verification and budget
reuse checks and the three-case timing catalog before staging.

Five jobs took 233.28 seconds on the quiet VM. Each used two processes per
revision, eight seconds of rotated warmup per process, and five measured rounds.
The intervals below show the smallest and largest changes between process
medians, in percent. Negative values favor the prototype.

| Script and phase | Elapsed change | Process CPU change | Same-image elapsed spread, reference / prototype |
| --- | --- | --- | --- |
| Helper text, compile | −0.06 to +0.72 | +0.72 to +1.76 | 0.70 / 0.08 |
| Helper text, warm | −2.15 to +2.38 | −3.08 to +3.75 | 0.39 / 4.23 |
| Helper text, compile and run | −0.77 to +1.11 | −1.45 to +1.60 | 0.31 / 1.58 |
| Floating JSON report, warm | −1.10 to +1.05 | −0.22 to +0.49 | 0.83 / 1.32 |
| Positional calls, warm control | −1.33 to +1.34 | −2.36 to 0.00 | 1.18 / 1.51 |

Warm includes mediated RAM I/O, checks and prior result cleanup. Compile and
run adds compilation and compiled-owner disposal. Whole-process CPU also
includes fixture reset and protocol overhead. Both revision images used default
GC and tiering. Separate warmed compilation allocation was identical across the
three scripts. Execution allocations varied by at most 0.018% across 13 scripts.
All accounted peaks matched and shared allowances returned to baseline.

Local Windows .NET 11 RC3 disassembly from the same verified images shows the
Tier1 native `Call` shrinking from 3,488 to 3,428 bytes. The reference's two
instructions that copy the result to a second local before publication disappear
from the prototype. This explains a code shape change. The Linux comparisons
establish neither a consistent gain on the target scripts nor the cause of the
4.23% difference between the helper prototype processes.

The prototype was withdrawn. The five readonly references would require future
callers to establish stable value lifetimes through input retirement. The
observed code shape change and inconclusive timings did not justify that extra
invariant. The higher compile CPU and ambiguous positional CPU remain visible.

The first read-only VM inventory timed out. A targeted check found four exact
committed observation replicas, which were verified again under the staging
lease before retirement. The first Windows diagnostic used an invalid fixture
prefix, then a needlessly large diagnostic batch timed out. The shorter
diagnostic preserved output verification and reached Tier1 on both images.
The initial errors and partial assembly are archived. All comparison workers
joined, RAM fixtures were removed, and the persistent observations were verified.

[Comparison data](summary.json) · [Allocation](allocation-summary.json) ·
[Assembly and checks](diagnostics.tar.xz) · [Raw observations](observations.tgz) ·
[Prototype source](../focused/README.md)
