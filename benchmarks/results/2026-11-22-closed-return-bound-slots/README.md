# B1404 Closed integer returns from bound defaults

Frozen `8c7da02be` and private `496994cd9` ran on November 22, 2026
UTC on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3.
Only the core DLL differs between the two verified 32-file images.

The private runtime uses the existing closed integer kernel after
ordinary binding fills the native parameters. It can return before
starting the interpreter activation. Live defaults, error order,
reflection, promotion and protocols still enter their existing
binding or dispatch paths. The arithmetic operand reader and the
first entry for fully supplied positional returns are unchanged.
The inspected factory code contains the expected three-instruction
closed body. Both images have matching closed-return eligibility.

| Complete script | Boundary | Elapsed change | Wider process CPU change | Worker spread old / new |
| --- | --- | ---: | ---: | ---: |
| Postponed nested factories | Warm | +0.71 to +4.19% | +1.70 to +4.69% | 2.79 / 0.64% |
| Postponed nested factories | Compile to result | −1.35 to +4.33% | −0.96 to +4.93% | 0.33 / 5.42% |
| Fully supplied helper | Warm | +0.40 to +1.09% | −0.15 to +1.46% | 0.30 / 0.40% |
| Eager nested factories | Warm | −4.32 to +0.89% | −3.89 to +1.25% | 1.55 / 3.83% |
| Factories observing default tuples | Warm | −3.65 to −1.98% | −4.29 to −2.06% | 0.88 / 0.84% |
| Quote-heavy CSV policy invoice | Warm | −1.44 to +2.09% | −1.12 to +2.69% | 1.87 / 1.68% |

Ranges span comparisons between two process medians per image after
eight seconds of full-request warmup and seven rotated batches.
Warm elapsed includes applicable mediated RAM-backed I/O, output
checks and prior-result disposal. Compile to result adds host
compilation and code-owner cleanup. Wider CPU includes resets and
protocol. Process startup and final teardown have separate clocks.

Keep the candidate private. The postponed factory takes longer warm
and uses more CPU, while the observer improves. The fully supplied
helper also has a small elapsed loss. These measurements do not
isolate native code generation or establish the cause of the
earlier positional regression. First-use elapsed spans zero with
5.42% candidate spread. Local allocation is effectively flat and
all admitted peaks match. All compiled and execution owners refund.
Preserve the adverse desktop observations, including the noisy
fully supplied result during local test activity.

Each platform passes 478 focused Release checks. The added case
matches CPython 3.13.2 after replacing live defaults, running a
reflected protocol, widening an integer, and checking missing
arguments before the arithmetic body. Both frozen images match
five complete CPython goldens. All 168 timed batches and both
quiet gates verify. Workers join, the guest mount is removed and
the persistent archive verifies under the lease.

[Source and images](source.json) · [Timings](summary.json) ·
[Allocation and admission](local-allocation.json) ·
[Raw observations](observations.tgz) · [Checks and procedures](procedures.tar.xz).
