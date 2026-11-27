# B1866 Share the iterable argument reader

The candidate uses the call accumulator's single positional reader for
`starmap` and starred calls. The two old starred drains disappear.
The shared reader lets same-command generators fill existing admitted
capacity and relinquishes the source cursor before invocation. The starred
call retains its own error translation and its original deferred expansion
order. The new regression holds previous aliases across a pending generator
read and checks cancellation, file closure and the allowance refund.

The reference contains `aae8992e0`. The candidate is that revision
plus the archived source patch. Only the core DLL differs between the
32-file images. Both match three independent CPython output and file
goldens. The starred report reads 2,048 records from the 27,350-byte
shipment fixture and passes a generator-produced row to each call.

On Windows .NET 11 Release, the starred report's CLR allocations
and peak shared admission match in both APIs. The controls differ by
at most 56 CLR bytes per invocation at equal peaks. All commands
restore their allowance baseline.

## Initial timing

Ubuntu WSL and .NET 11 RC3, one logical CPU. Two workers per image warm
for at least five seconds and contribute seven rotated batches.
Elapsed includes verification, applicable RAM-backed I/O and previous
result disposal. Process CPU also covers fixture reset and request framing.
Ranges span pairings of worker medians. Startup and compilation are outside
these clocks.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Starred generator rows | Sync | −8.22% to −1.85% | −8.48% to −2.86% |
| Iterable-row `starmap` control | Async | −4.92% to −1.17% | −7.50% to −4.99% |
| Starred generator rows | Async | +16.92% to +20.62% | −9.68% to +9.80% |
| File-fed map control | Async | −22.28% to +26.17% | −32.77% to −0.77% |

All 112 batches verify. The file-fed reference varies by 55.83% on
elapsed and 36.02% on CPU. The adverse async elapsed and the moving
controls warrant a bounded quiet repeat of the same images.

Each Windows Release target passes 388 public and 109 white-box checks
plus both outcomes of the new pending-read regression. CPython verifies
that regression's output and effect order. Ubuntu WSL .NET 10 passes
all 499 focused checks after a retry of the initial stopped restore.

## Quiet confirmation

The same images run in reverse order on the Azure F4as_v7 with
Ubuntu 24.04.4 and .NET 11 RC3. The worker count, goldens, clocks
and sample schedule stay fixed.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| File-fed map control | Async | −5.09% to +0.04% | −5.11% to +0.65% |
| Starred generator rows | Async | −11.01% to −7.57% | −10.22% to −9.29% |
| Iterable-row `starmap` control | Async | +0.46% to +1.13% | 0.00% to +1.41% |
| Starred generator rows | Sync | −5.18% to −4.30% | −5.38% to −3.55% |

Retain the shared reader. It removes both separate drains and the quiet
trial records lower CPU in both affected lanes. The initial async elapsed
increase reverses. The `starmap` control moves favorably in the first run
and takes slightly longer in the quiet repeat, with CPU reaching parity.
File-fed timing spans zero in the quiet repeat.

Both quiet gates and all 112 batches pass in 162.75 seconds. Workers
join and persistent observations verify after guest files are removed.
Staging frees 5.84 MB from a retired worker replica after verifying
its complete frozen local image and original archive.
[Storage record](quiet/vm-storage.json).

[Local timing](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Image identity](identity.json) · [Observations](observations.tar.xz) ·
[Exact source and checks](procedures.tar.xz) · [Quiet timing](quiet/summary.json) ·
[Quiet observations](quiet/observations.tar.xz) ·
[Quiet terminal status](quiet/terminal-status.json).
