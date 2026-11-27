# B1899 Borrow the established lookup array in each frame

The prototype lets a frame replace its reference to the function's
lookup owner with the existing array. Both shared and private arrays
then use the same access and clear path. Cold admission still uses the
function owner, with the existing cancellation and reentry checks.
The execution retains the shared charge. No frame fields are added.

The reference is `55b14a0c3`, with production source from `5cb61871a`.
Both frozen 32-file images differ only in the core DLL. Both pass four
CPython 3.13.16 output and file goldens. Windows Release passes 83
focused checks on each of .NET 10 and .NET 11. These include interleaved
generator layouts, live record mutation, cancellation, terminal refusal
and suspended callbacks. Allocation is effectively flat and admitted
peaks match across both APIs. All budgets return to baseline.

On November 27, 2026 UTC, Ubuntu WSL runs the original shipment fixture
on CPU 27 with .NET 11 RC3. Two workers per image warm for five seconds
each and produce seven rotated batches per worker. Ranges span all
pairings of worker medians. Warm elapsed includes mediated RAM-backed
I/O, output and file verification, and previous-result disposal.
Process CPU covers fixture reset and request framing as well.

| Warm script | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Iterable-row `starmap` | `Run` | −1.93% to +11.25% | −6.04% to +8.48% |
| Bound `starmap` | `RunAsync` | −7.68% to +3.26% | −5.87% to +0.83% |
| Wide-list `starmap` | `RunAsync` | −2.48% to +0.03% | −0.47% to +0.89% |
| Iterable-row `starmap` | `RunAsync` | −1.84% to +1.89% | −3.45% to +1.54% |

All 112 batches verify. Sync iterable CPU spread reaches 7.72% in the
reference image and 7.18% in the candidate. All CPU ranges span zero.
Withdraw the prototype. The cached reference changes frame state after
first use and adds a small amount of code, with no demonstrated gain
in either admission or whole-script cost. Keep B1897's shared backing.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Image identity](identity.json) · [Observations](observations.tar.xz) ·
[Patch, goldens and checks](procedures.tar.xz).
