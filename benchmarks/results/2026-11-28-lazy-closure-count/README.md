# B1955 Count closure bindings without allocating a namespace

An executable frame can keep its locals entirely in admitted slots.
Retention accounting now reads the count of its existing namespace
entries without creating a dictionary for a frame that has none.
The same count and slot capacity still fund retained bindings. Real
namespace users continue to share a stable dictionary.

The Windows .NET 11 allocation witness saves 163,840 B per ordinary
tuple reduction in both APIs. The range reduction saves 163,784 B sync
and 163,840 B async. The two controls differ by at most 56 B. Compiled
reservations, execution peaks and growth-request counts match, and every
invocation returns its allowance to baseline. The ordinary reduction
saves 4.73% of the CLR allocations measured in this witness.

Windows .NET 10 and 11 and Ubuntu WSL .NET 11 each pass 857 public
and 285 white-box focused Release checks. The checks cover retained
namespace aliases, counting actual bindings, generator cleanup,
cooperative cancellation and terminal refusal. Both frozen 32-file
images verify four CPython 3.13.16 output and file goldens. The core DLL
is the only difference from the reference at `2806d7d75`.

## Local warm comparison

November 28, 2026 in Ubuntu WSL on .NET 11 RC3, pinned to one logical
CPU. Two workers per image warm for five seconds each and contribute
seven rotated batches. Ranges pair all worker medians. Elapsed includes
applicable RAM-backed I/O, verification and previous-result disposal.
Process CPU also includes fixture reset and framing. Both clocks
exclude compilation and startup.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Ordinary tuple reduction | `Run` | −1.53% to +0.95% | −1.18% to +1.02% |
| Ordinary tuple reduction | `RunAsync` | −3.12% to −1.38% | −2.89% to −1.24% |
| Range reduction | `RunAsync` | −4.89% to +1.97% | −5.71% to −0.29% |
| Iterable shipment | `RunAsync` | +0.50% to +2.34% | +0.20% to +1.25% |
| File-fed quantity map | `RunAsync` | −2.95% to +4.03% | −0.48% to +1.67% |

All 140 timed batches and cleanup pass. Keep the allocation removal and
the favorable ordinary async CPU comparison. Range CPU is favorable
with 5.41% reference spread. Ordinary sync and the file-fed control
span zero, and iterable CPU rises slightly. Preserve those outcomes.

[Timing](summary.json) · [Allocation](allocation.json) ·
[Admission](admission.json) · [Verified images](identity.json) ·
[Observations](observations.tar.xz) · [Source and checks](procedures.tar.xz).
