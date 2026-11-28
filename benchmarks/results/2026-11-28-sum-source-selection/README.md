# B1949 Select the generator source before reduction

Builtin `sum` selects its optional private frame or buffered public
generator before entering the reduction loop. Ordinary cursors take
the initial branch. `RunAsync` selects the pull and awaits it at one
site. The loop keeps the public generator object across callbacks so
its next pull observes reentry and exhaustion. Cursor cleanup, frame
ownership, guest numeric protocols and cancellation keep their existing
boundaries.

The frozen candidate and `df568572c` reference differ only in the core
DLL. Emitted IL confirms that the async pull has one await site, down
from three. Its state machine's `MoveNext` shrinks from 1,553 to 1,333 IL
bytes and keeps 19 fields. Synchronous `Sum` grows from 325 to 338 IL bytes.
Compiled and peak execution admission match, and all commands refund to
baseline. CLR allocation differs by at most 174 B.

Windows .NET 10 and 11 and Ubuntu WSL .NET 11 each pass 738 public and
193 white-box focused Release cases. Both 32-file images verify all four
stock CPython 3.13.16 output and file goldens locally and on the VM.

## Quiet warm comparison

The November 28, 2026 UTC run uses the Azure F4as_v7 VM, Ubuntu 24.04.4
and .NET 11 RC3. Two workers per image warm for five seconds and supply
seven rotated batches per lane. Ranges pair all worker medians. Elapsed
includes mediated RAM-backed I/O, output and file verification, and
previous-result disposal. Process CPU adds fixture reset and framing.
Compilation and startup are outside these clocks.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| File-fed CSV quantity map | `Run` | −0.32% to +0.57% | −0.42% to +2.77% |
| File-fed CSV quantity map | `RunAsync` | −6.66% to −3.64% | −6.06% to −3.48% |
| Ordinary tuple reduction | `Run` | −2.91% to +1.99% | −2.51% to 0.00% |
| Ordinary tuple reduction | `RunAsync` | −1.34% to +0.93% | −0.21% to +1.48% |
| Iterable shipment | `RunAsync` | −3.41% to −0.95% | −2.35% to +0.22% |

All 140 batches and both quiet gates pass. The VM reuses 63 immutable
donor files. Workers join, fixtures are removed and evidence verifies
under the lease. Retain the shared pull await and the measured async
file-fed improvement. Ordinary and synchronous timings remain
inconclusive. The paired change measures both dispatch and await sharing.
Their individual contributions remain open. A separate comparison with
the original regression baseline spans zero. [B1950](baseline/README.md).

The existing `bf10eeaea` B1943 profile places `MemoryGovernor.Reserve`
nearest in 9.76% of resolved ordinary async reduction samples. The caller
split shows 2.47% of total samples under executable slot construction,
1.51% under `sum`, and 1.42% under lookup-table acquisition. The shares
locate several distinct storage lifetimes. A generator expression currently
acquires a frame-owned table at its first cached lookup. Generator functions
can reuse a definition-owned table. This suggests a way to eliminate a
repeated allocation. Its timing impact remains unmeasured. [Caller attribution](reserve-callers.json) ·
[Original profile](../2026-11-28-cpython-sequence-iterator-checkpoint/README.md).

[Timing](summary.json) · [Allocation](allocation.json) · [Emitted IL](il.json) ·
[Image identity](identity.json) · [Observations](observations.tar.xz) · [Procedures](procedures.tar.xz).
