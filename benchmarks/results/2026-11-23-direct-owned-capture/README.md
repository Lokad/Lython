# B1463 Retain the value of an unchanging capture in its callable

`f9c476e27` retains the production source measured at `6aee32253`
against `4997c6a59`. One existing capture reference holds an admitted
value for a sole unchanging parameter of the immediate parent. The
compiler reuses its parameter-write and nested-nonlocal facts. Forwarded,
writable and namespace-dependent bindings use ordinary shared cells.
Native execution, resumed execution and locals snapshots read the same
capture storage. The singleton cell's list interface and separate reader
dispatch are removed.

A diagnostic build observed 6,144 unpublished integer slots in each
factory across three verified invocations. The retained candidate
reserves storage before boxing the value in the authoritative parent
slot, so later reads keep one identity. Boxing now happens when the
child is created, even if the child is never called. The cell allocation
is removed. One positive request per factory input falls from 256 to
224 bytes, and both images still make 6,169 positive requests per run.

Only the core differs across the two verified 32-file images. The quiet
Azure F4as_v7 ran Ubuntu 24.04.4 with .NET 11 RC3 on November 23, 2026 UTC.
Two independent processes per source warm for eight seconds and supply
seven rotated batches each. Ranges cover all four pairings of worker
medians. Warm elapsed includes applicable mediated RAM-file I/O, output
verification and previous-result disposal. Time to result adds compilation
and compiled-owner disposal. Wider CPU also includes reset and protocol
work. Startup and final teardown sit outside these clocks.

| Complete script | Boundary | Elapsed change | Wider process CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Eager nested factories | Warm | +0.55 to +3.79% | +0.00 to +4.25% | 1.17 / 2.03% |
| Nonlocal word report | Warm | −9.12 to +4.28% | −8.84 to +1.21% | 6.55 / 7.69% |
| Fully supplied helper | Warm | +1.59 to +3.93% | −0.78 to +2.79% | 1.32 / 0.97% |
| Postponed nested factories | Warm | −0.05 to +4.06% | −2.20 to +6.79% | 2.57 / 1.50% |
| Wide positional calls | Warm | −1.85 to +0.38% | −1.32 to +0.88% | 0.13 / 2.14% |
| Defaults observer | Warm | −5.02 to +1.90% | −5.04 to +1.33% | 0.75 / 6.48% |
| Postponed nested factories | Compile to result | −7.55 to −2.03% | −6.55 to −1.72% | 4.34 / 1.56% |
| Quoted CSV invoice | Warm | −0.03 to +1.05% | −0.70 to +2.50% | 0.46 / 0.62% |

Retain the eliminated allocation and faster postponed time to result.
Carry the eager warm and fully supplied elapsed regressions into the next
call checkpoint. The postponed warm and defaults-observer ranges span
zero. The nonlocal text workers differ by more than 6.5% on each source,
leaving that control inconclusive. These data compare Lython sources.

Each factory allocates 114,688 fewer managed bytes, about 13.0%. The defaults
observer saves the same bytes, 9.35% of its reference allocation. The three
admitted peaks fall by 65,536 bytes each. Other admitted peaks match, and
measured allocation on the text control differs by 168 bytes. The budget
returns to baseline after cleanup.

Windows .NET 11 and Ubuntu .NET 10 each pass 165 white-box and 879 public
focused checks. The final 33-case public subset adds cancellation while
the captured value is in use. It verifies joined file cleanup and funding
until that cleanup completes. The two images verify seven CPython goldens
and identical executable tapes across 149 inspected code objects. A separate
CPython check verifies the captured parameter's identity and its
`locals()` value across file reads. Procedures preserve the initial
probe framing and build failures.

All 224 timed batches verify in 373 seconds. Workers join, guest fixtures
are removed and the raw archive verifies under the exclusive lease.
The superseded B414 working DLL was copied to persistent local storage
and checked against its recorded hash before staging retired the VM copy.

[Source and images](identity.json) · [Timing](summary.json) ·
[Allocation and peaks](allocation-summary.json) · [Admission requests](admission.json) ·
[Representation probe](representation-evidence.json) · [Executable parity](executable-parity.json) ·
[Raw observations](observations.tar.xz) · [Checks and procedures](procedures.tar.xz) ·
[Terminal status](terminal-status.json).
