# B1504–B1505 Return a supplied scalar before native activation setup

`a2bde38b2` retains private source `73e28cac2`. The ordinary executable
caller and the native prefix use the same supplied-operand check for
an exact integer return. A successful call uses the caller's existing
slots and skips the general starter, its managed activation workspace
and the optional unpack destination. The bound receiver uses the same
entry. The integer evaluator, recursion checks, traceback and ordinary
result publication stay shared. Other operands and overflowing arithmetic
use the existing general continuation.

Both comparisons use `b32bf6b6b` and the same verified 32-file candidate
on the quiet Azure F4as_v7, Ubuntu 24.04.4 with .NET 11 RC3, on November
23, 2026 UTC. Two processes per revision warm for eight seconds and
supply seven rotated batches. Ranges pair their medians. Warm elapsed
includes applicable mediated RAM-file I/O, golden verification and
prior-result disposal. Compile to result adds compilation and code-owner
disposal. Wider CPU includes fixture reset and protocol. Startup and
final teardown are outside elapsed.

| Workload | Phase | Elapsed change | Wider CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Supplied arithmetic, module scope | Warm | −4.15 to −3.35% | −4.01 to −2.41% | 0.51 / 0.33% |
| Supplied arithmetic, function scope | Warm | −2.86 to −1.92% | −3.45 to −2.12% | 0.79 / 0.17% |
| Nested-helper CSV invoice | Warm | −0.74 to +1.53% | −0.61 to +1.54% | 0.62 / 1.66% |
| Postponed nested factories | Warm | −0.29 to +11.41% | −0.45 to +10.60% | 0.56 / 11.12% |
| Quote-heavy CSV invoice | Warm | −4.68 to +2.25% | −4.09 to +1.79% | 5.26 / 1.91% |
| Supplied arithmetic, module scope | Compile to result | −3.20 to −2.30% | −3.26 to −2.71% | 0.90 / 0.03% |
| Postponed factories, B1505 repeat | Warm | −7.65 to +1.33% | −8.89 to +1.45% | 8.22 / 1.39% |
| Quote-heavy invoice, B1505 repeat | Warm | +0.98 to +1.77% | −1.42 to +2.50% | 0.34 / 0.45% |
| Supplied arithmetic, B1505 repeat | Warm | −4.23 to −1.63% | −4.25 to −1.03% | 1.01 / 1.69% |

Retain the shared scalar entry for the removed setup and reproducible
supplied-call gains. The first postponed comparison has one slower
candidate worker. The repeat has a moving reference and spans zero.
Keep this uncertainty and the repeat's quoted-invoice elapsed increase
with mixed CPU. Both invoices remain controls for subsequent call changes.
The Windows diagnostic compiles the general starter in the reference
and only the supplied scalar entry in the candidate on the module helper.
Local allocated bytes match on both supplied workloads. All five reported
admitted peaks match and the borrowed allowance returns to baseline.

Windows .NET 11 and Ubuntu .NET 10 each pass 373 white-box and 688 public
focused Release checks. The two complete images verify five workload
goldens and the independent supplied scalar, bound receiver, overflow and
unpack-error CPython case. All 168 initial and 84 repeat timed batches
verify in 281 and 143 seconds respectively. Both leases end after workers
join, guest fixtures are removed and the persistent archives verify.

[Identity](identity.json) · [First timing](summary.json) · [Repeat timing](repeat-summary.json) ·
[Allocation](allocation-summary-six.json) · [JIT entry](jit-entry-summary.json) ·
[First observations](observations.tar.xz) · [Repeat observations](repeat-observations.tar.xz) ·
[First terminal status](terminal-status.json) · [Repeat terminal status](repeat-terminal-status.json) ·
[Checks and procedures](procedures.tar.xz) · [Exact reference core](../2026-11-23-eager-isolated-definition/README.md).
