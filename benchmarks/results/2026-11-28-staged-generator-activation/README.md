# B1936 Hand the private generator frame to starmap

When a same-command generator yields the result of an adjacent positional
call to a generator function, a compatible `starmap` pull can take the
initialized frame. The call admits and binds that frame before the
producer reaches its usual yield check. Both public generator objects
and private frames use the same sync and async advancement.

The private frame stays owned through a pending pull. The general binder
receives any prefetched values and materializes the generator when it
needs to continue after an excess yield. Ordinary iteration still
publishes generator objects. The focused checks exercise the private
handoff, public identity, defaults, excess values, suspended I/O,
cancellation and shared-allowance cleanup. Windows .NET 10 and 11 and
Ubuntu WSL .NET 11 each pass 598 public and 139 white-box Release checks.
The frozen reference at `2fd82139e` and the refined candidate match all
four CPython 3.13.16 output and file goldens. Their 32-file images differ
only in the core DLL.

## Allocation

The first variant adds separate storage for the staged activation and
suspended exception to each frame. It saves 81,880 B per iterable
shipment and adds 81,944 B per ordinary reduction in each API. The
refinement reuses the yield target and frame-entry exception storage
across their respective nonoverlapping lifetimes.

| Workload | `Run` CLR allocation change | `RunAsync` CLR allocation change |
| --- | ---: | ---: |
| Iterable shipment | −131,024 B | −131,080 B |
| Ordinary helper and generator reduction | −16,384 B | −16,384 B |
| File-fed map | −56 B | 0 B |
| First-line file search | 0 B | 0 B |

The separate Windows .NET 11 witness finds equal compiled and peak
execution charges in all cases. Every command returns the allowance
to baseline. The first variant was withdrawn before timing.

## Warm execution

The November 28, 2026 local Ubuntu WSL run uses .NET 11 RC3 and one
logical CPU. Two workers per image warm for five seconds and then
alternate seven timed batches. The ranges pair worker medians. Elapsed
includes applicable RAM-backed I/O, verification and prior-result
disposal. Process CPU also includes fixture reset and framing.
Compilation and startup are outside both clocks.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Iterable shipment | `Run` | +0.78% to +1.76% | +0.81% to +1.47% |
| Iterable shipment | `RunAsync` | −8.41% to −3.33% | −9.27% to −2.68% |
| Ordinary helper and generator reduction | `RunAsync` | −3.20% to +2.03% | −2.29% to −0.79% |
| File-fed map | `RunAsync` | −1.27% to +0.13% | −2.11% to −0.13% |
| First-line file search | `RunAsync` | −4.28% to +18.12% | −9.86% to +14.42% |

All 140 timed batches verify, and workers and fixtures are cleaned up
before archive verification. Retain the frame transfer for the measured
6.2% allocation reduction in both APIs and lower async iterable CPU.
The async reference workers differ by 5.73% CPU. The first-line reference
control differs by 20.81%, so its timing is inconclusive. Keep the sync
slowdown visible and recheck both APIs at the next quiet checkpoint.

[Timing](summary.json) · [Allocation](allocation.json) ·
[First variant allocation](initial-allocation.json) · [Image identity](identity.json) ·
[Observations](observations.tar.xz) · [Source patches and procedures](procedures.tar.xz).
