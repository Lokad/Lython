# B1334 Transport parsed CSV doubles through fixed-unpack slots

Private `52d80f93b` writes a parsed numeric field directly into the
existing executable slot. Ordinary row creation still boxes each value
for its list. Both consumers share string materialization. The private
slot defers boxing until an operation observes the value. Field
admission, arity, allocation rollback and retained backing stay intact.

Compared `52eeb328e` and `52d80f93b` on November 22, 2026 UTC on the
quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. Two processes
per revision warm for at least twelve seconds and supply seven rotated
batches. Ranges span all four pairings of process medians. Warm time
includes mediated RAM-backed I/O, output verification and prior-result
cleanup. Time to result also includes compilation and code-owner
disposal. Process CPU includes the wider request. Startup and final
teardown are separate.

| Complete script | Boundary | Elapsed change | Process CPU change | Elapsed spread reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Typed ASCII meter report | Warm | −3.70 to +0.22% | −3.64 to +0.92% | 3.13 / 0.92% |
| Explicit-float meter report | Warm | +0.15 to +2.49% | +0.06 to +3.15% | 1.75 / 0.57% |
| Typed Unicode meter report | Warm | +0.47 to +4.66% | 0.00 to +5.15% | 0.58 / 3.57% |
| Plain integer invoice | Warm | +3.16 to +16.20% | +2.94 to +16.99% | 0.35 / 12.25% |
| Plain integer invoice | Warm async | −1.70 to −0.26% | −0.97 to +0.54% | 0.64 / 0.82% |
| Typed ASCII meter report | Time to result | −1.82 to −0.84% | −2.13 to −0.95% | 0.31 / 0.68% |

Local allocation falls 6.89% on the two typed reports, by 196,608 and
196,664 bytes respectively. The explicit-float and invoice allocations
match exactly. Admitted peaks match and budgets refund across all four
scripts. These diagnostics measure allocation rather than elapsed time.

Keep the prototype private. ASCII time to result improves, but warm
Unicode slows and the unchanged synchronous invoice shows a large
candidate-process spread and adverse medians. The comparison does
not identify the cause. Retain this warning for future changes to
operand publication. The next candidate targets the positional numeric
call path independently of CSV slot ingress.

Windows .NET 11 and Ubuntu .NET 10 each pass 279 white-box and 1,124
public focused Release checks. Both 32-file images match four CPython
goldens and all 168 timed batches verify. Workers join, guest fixtures
are removed and the archive verifies under the exclusive lease. Initial
local staging hit a test-log check, a receipt variable collision and
insufficient proved spare capacity. The final staging retires only the
two working copies whose identical contents survive in pushed archives.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Source and checks](checks-and-procedures.tar.xz).
