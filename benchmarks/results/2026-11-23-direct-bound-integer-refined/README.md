# B1431 Read prepared integer operands directly

Private `e0957b670` refines B1430 by declining exposed keyword defaults
before inspecting the positional tuple. Fully supplied calls use their own
operand reader and skip all default handling. Eligible omitted arguments
come directly from the existing default owners. Both paths share the
checked integer evaluator and skip binder workspace. Invalid arity, general
Python operands, live keyword mappings and wide arithmetic continue through
ordinary binding or activation. Managed stack admission and cancellation
stay in place. The verified source change is retained as `95b28daaf`.

Compared with the exact B1429 `1b841b597` core on November 23, 2026 UTC on
the quiet Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3. Only the core differs
across the two verified 32-file images. The first worker order reverses B1430.

| Complete script | Boundary | Elapsed change | Wider process CPU change |
| --- | --- | ---: | ---: |
| Postponed nested factories | Warm | −6.79 to −0.53% | −5.51 to −0.31% |
| Postponed nested factories | Compile to result | −7.97 to +2.71% | −9.39 to +2.88% |
| Eager nested factories | Warm | −2.66 to −0.94% | −3.19 to −0.97% |
| Factories reading defaults metadata | Warm | −1.49 to +0.31% | −2.81 to +1.54% |
| Fully supplied helper | Warm | −1.88 to +0.38% | −1.15 to +0.88% |
| Quote-heavy CSV invoice | Warm | −0.50 to +1.77% | 0.00 to +2.25% |

Ranges span four pairings of independent worker medians. Each worker warms
for eight seconds and supplies seven rotated batches. Warm elapsed includes
applicable mediated RAM-backed I/O, verification and previous-result disposal.
Time to result adds compilation and compiled-owner disposal. Wider CPU includes
resets and protocol. Startup and final teardown sit outside elapsed.

Both factory warm gains recur. Postponed warm candidate spread is 5.54%
against 1.11% reference spread, and both first-result worker spreads exceed
5.6%. The earlier defaults-observer loss does not recur. Fully supplied timing
now spans zero, with 0.47% reference and 1.83% candidate spread. The first
quoted-invoice improvement does not recur either. Retain the shared evaluator
and direct reads with first-result uncertainty and the possible invoice CPU
cost recorded. Local allocation is effectively flat, admitted peaks match
and allowances return after cleanup. No new CPython elapsed ratio was measured.

Five complete scripts match CPython goldens. Separate CPython parity probes
cover missing unused parameters, default replacement, keyword-key comparison,
reflected addition, large defaults tuples, wide promotion and signed or zero
modulo. Windows .NET 11 and Ubuntu .NET 10 each pass 405 focused Release
checks. Local JIT diagnostics emit both reader bodies with tiering disabled.
Quiet timings run with normal tiering.

All 168 batches and both quiet gates pass in 329 seconds. Workers join,
fixtures are removed and the persistent archive verifies under the lease.
Staging retires only byte-verified replicas preserved in earlier archives.

[Source and images](source.json) · [Timings](summary.json) ·
[Allocation and admission](local-allocation.json) · [Observations](observations.tgz) ·
[Checks and procedures](procedures.tar.xz) · [Archive receipt](archive-receipt.json) ·
[B1430 initial trial](../2026-11-23-direct-bound-integer/README.md) ·
[Reference checkpoint](../2026-11-23-retained-cpython-checkpoint/README.md).
