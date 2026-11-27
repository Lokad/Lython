# B1845 Active CPU after the shipment checkpoint

The quiet Azure F4as_v7 captured `a1b3a3325` on November 27, 2026 UTC
with Ubuntu 24.04.4 and .NET 11 RC3. Both async scripts read 2,048 CSV
records from a 27,350-byte fixture. They use the B1844 worker image and
the comparison host from `05c1059de`. Both output and file goldens match
independent CPython runs on Windows and stock CPython 3.13.16 on the VM.

Each worker warms for at least eight seconds of command time. Linux
`perf` requests a six-second active CPU capture at 199 Hz. It covers
mediated RAM-backed I/O, fixture reset, validation, response framing and
cleanup. Counts use the nearest resolved runtime frame and include work
inlined into it. They identify candidate stages and do not measure elapsed
improvements or explain the entire CPython gap.

| Observation | Wide shipment | File-fed map |
| --- | ---: | ---: |
| Verified invocations during capture | 3,072 | 12,288 |
| All active CPU samples | 1,436 | 1,234 |
| Unresolved leaf samples | 138 | 272 |
| Samples with resolved runtime ancestry | 1,386 | 1,066 |
| Nearest executable instruction prefix | 241 | 1 |
| Nearest fixed-unpack advance or completion | 100 | 0 |
| Nearest converted dictionary update | 58 | 0 |
| Nearest starmap advance | 49 | 0 |
| Nearest map advance | 36 | 146 |
| Nearest CSV line read | 55 | 133 |
| Nearest CSV record emission | 26 | 124 |
| Nearest DictReader async row state machine | 14 | 78 |
| Nearest direct field-call return | 0 | 78 |

The ready starmap entry remains visible. Its former per-row state machine
had 47 nearest samples in [B1836](../2026-11-27-current-shipment-cpu/README.md)
and has zero in this capture. The profiles contain different sample totals
and other runtime changes. Wide shipment still concentrates in dispatch
and unpacking. The capture points to a complete stage in the file-fed map
path whose removal can be tested. The CSV parser can return a finished
dictionary directly, but the reader enters the general row state machine
before discovering that result. Test a shared ready prefix that leaves
header protocols and raw row ownership in the existing completion path.
[B1484](../2026-11-23-dictreader-activation/README.md) preserved an older,
broader split privately after mixed whole-script results. B1842 extended
direct publication to iterable input after that experiment.

Staging reused all 32 verified immutable worker files and streamed 123,869 B
of new inputs. A verified, restorable copy of one old .NET build-cache
entry recovered 75,087,872 B before staging. Existing archived images and
evidence stayed in place. The six-MiB persistent free-space floor was met.
The service passed both quiet gates, joined the workers and profilers,
removed the RAM-backed fixtures and verified its persistent archive.

[Attribution](attribution.json) · [Image](identity.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Staging](stage-custody.json) · [Procedures and verification](procedures.tar.xz).
