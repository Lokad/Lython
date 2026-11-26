# B1773 Literal defaults without execution staging

This comparison applies one patch to `dc736119e`. The compiled declaration
holds the same immutable positional tuple. Execution reads it when the
function is created, removing its operand load and the copy into a deferred
local. An eligible deferred definition with that tuple can hold its prepaid
keyword default in its final local slot. Computed defaults retain their
evaluation order and existing admission. A namespace observer still receives
real functions.

The ordinary factory has 32 literal positional default tuples among 96
definitions and runs 16 times per result. Its 93 deferred sites still defer.
Thirty sites need one fewer workspace slot, reducing the factory from 194
slots to 164. The ordinary factory allocates 480 fewer managed bytes per
execution and peaks at 92,040 admitted bytes, down 720 B. Compilation allocates
1,008 fewer bytes and holds 512 fewer admitted bytes. The scope observer peaks
24 B lower. Both controls keep their recorded peak admission. The complete
allocation diagnostics include small increases on the shipment control and
24 additional CLR bytes at compile time on each control. [Reference shape](reference-shape.json) ·
[Candidate shape](candidate-shape.json) · [Execution allocation](allocation-summary.json) ·
[Compilation allocation](compile-allocation-summary.json)

Both frozen images pass four independent CPython goldens. Windows .NET 11
passes 1,467 focused Release checks and Ubuntu WSL .NET 10 passes 1,448. The
existing cases exercise default identity, replacement, order, alias visibility,
host suspension, admission and allowance reuse. [Patch, golden results and test logs](procedures.tar.xz)

The first block uses local Ubuntu WSL .NET 11 RC3 on November 26, 2026 UTC.
Two processes per image and lane warm for three seconds each. The seven
rotated batches per process target 320 ms. Changes span all four pairings of
process medians. Negative changes mean lower time. Elapsed includes scripted
execution with verified output, applicable I/O and cleanup. The combined lane
also includes compilation and compiled-owner disposal. Process CPU also covers
the request boundary. [Medians and same-image spreads](summary.json) ·
[Raw observations](observations.tar.xz) · [Frozen image hashes](identity.json)

| Workload | Lane | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Shipment control | Warm async | −2.60% to +3.31% | −7.27% to +15.65% |
| Ordinary definitions | Warm sync | −48.49% to −10.86% | −17.13% to +2.04% |
| Escaping rules | Compile-to-result | −13.52% to +6.42% | −27.78% to +11.73% |
| Ordinary definitions | Warm async | −11.69% to −6.14% | −14.69% to −0.52% |
| Scope observer | Warm sync | +0.54% to +6.89% | −9.52% to −2.28% |
| Ordinary definitions | Compile-to-result | −76.02% to +44.41% | −57.96% to +2.56% |
| Escaping rules | Warm sync | +3.05% to +5.34% | −2.69% to +4.67% |

The local compile-to-result spread reaches 305% on the same reference image.
Shipment CPU crosses zero and escaping-rule elapsed rises with little
same-image elapsed spread. This block establishes the storage change and
leaves the timing decision open.

The quiet confirmation supports keeping the simpler storage. It finds lower
warm sync CPU and higher warm async CPU on ordinary definitions. Escaping-rule
elapsed crosses zero. [Quiet comparison and delivery decision](confirmation/README.md)
