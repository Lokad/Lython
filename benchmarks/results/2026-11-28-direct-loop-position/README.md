# B1953 Keep an exact sequence position in its executable operand

The trial stores the source sequence and its live index in the existing
loop slot. The native prefix and full interpreter use the same representation.
The full interpreter drops its position array and the prefix drops its managed
cursor for exact lists and tuples. A promoted call transfers the populated slot.
The already-acquired Python iterator for a generator-expression outer is
preserved, including the `.0` binding exposed by `locals()`.

The first trial removed only the position array. The repeated CSV helper
still allocated its prefix cursor. The refined image removes both forms of
backing. The ordinary report uses one fewer growth request and peaks 52 B
lower in both APIs. The repeated helper saves 180,232 B in `Run` and
180,058 B in `RunAsync`, about 17.1%, at matching peak admission. Report
allocation varies by about 2 KB in the local witness. The file-fed control
differs by 8 B. Compiled reservations match and all owners refund to baseline.

The first focused run failed four tests that expected refusal of the discarded
array. The updated tests require the funded loops to run with zero headroom
and keep their sequence, side effects and cleanup checks. The range construction
refusal test still exercises admission of real new storage. New public coverage
checks nested positions and live list mutation across an asynchronous host read.
Windows .NET 10 and 11 and Ubuntu WSL .NET 11 each pass 1,057 public and
278 white-box focused Release checks on the refined image.

## Local warm comparison

November 28, 2026 in Ubuntu WSL on .NET 11 RC3, with one logical CPU.
Two workers per image warm for five seconds each and contribute seven rotated
batches. Ranges pair all worker medians. Elapsed includes mediated RAM-backed
I/O, verification and previous-result disposal. Process CPU also includes
reset and framing. Both exclude compilation and startup.

The repeated CSV helper performs a two-key tuple loop on each of 2,048 rows.
The report renders 3,072 JSON records into a Markdown file. The unchanged
streaming comparison uses a file-fed CSV quantity map. The two frozen 32-file
images differ only in the core DLL, with `60ebd6274` as the reference. Both
verify all three CPython 3.13.16 output and file goldens. All 140 timed batches
verify and the observation archive verifies after worker and fixture cleanup.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Repeated tuple loop | `Run` | +8.95% to +11.31% | +8.53% to +13.92% |
| Repeated tuple loop | `RunAsync` | −6.96% to +1.72% | −6.77% to −1.42% |
| JSON report rendering | `Run` | −2.80% to +0.52% | −1.36% to +0.16% |
| JSON report rendering | `RunAsync` | −5.31% to +2.62% | −2.74% to +2.79% |
| File-fed quantity map | `RunAsync` | −0.69% to +3.66% | −0.24% to +3.79% |

The reference sync workers differ by 0.97% CPU and the candidate workers by
3.96%. The quiet repeat uses the exact images and moves the sync lane after
the async lane, with a same-mode streaming control.

## Quiet repeat

The exact images run on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and
.NET 11 RC3, against the same three goldens. Both quiet gates and all 84
timed batches pass. The collection uses the same timing boundaries and
rotated worker medians. Each worker joins and removes its fixtures before
archive verification.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| File-fed quantity map | `Run` | −1.47% to −0.32% | −1.12% to +0.48% |
| Repeated tuple loop | `RunAsync` | −13.43% to −0.62% | −12.98% to +0.81% |
| Repeated tuple loop | `Run` | −9.59% to −6.70% | −9.57% to −6.13% |

The quiet sync result reverses the adverse WSL result. Sync CPU spreads are
1.66% reference and 2.11% candidate. Async CPU spans zero with 7.02% and
8.25% worker spreads. The control's CPU also spans zero. Retain the simpler
ownership and exact allocation improvement, alongside both timing outcomes.
[Quiet summary](quiet/summary.json) · [Quiet observations](quiet/observations.tar.xz) ·
[Quiet source and checks](quiet/procedures.tar.xz).

[Timing](summary.json) · [Admission](admission.json) · [Allocation](allocation.json) ·
[Array-only trial](array-only-allocation.json) · [Verified images](identity.json) ·
[Observations](observations.tar.xz) · [Source and checks](procedures.tar.xz).
