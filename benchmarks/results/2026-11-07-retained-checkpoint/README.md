# Qualified retained-source CPython checkpoint — 2026-11-07

Source **55e510ff**, frozen core **83af401e**. The recorded 561-file production
inventory and focused core align with this source; informational SourceLink is
older than the commit. R49/R50/R51 have passed their full Windows/Ubuntu Release,
actual .NET 11/10, original Stress and installed core-package checks. The final
[delivery receipt](../2026-11-07-wildcard-translation/delivery-ci.json) qualifies
the production source; later documentation commits do not change the core.

B409 completes **23 jobs in 477.00 seconds**: seven maintained ordinary scripts
with compile, warm execution and compile-to-result separately, plus two explicit
integer-input warm cases. CPython 3.13.16 and .NET 11 RC3 on the same dedicated
Azure Standard_F4as_v7, Ubuntu 24.04.4, four AMD EPYC 9V45 vCPUs, 16 GiB/no swap.
Normal GC/tiering, ordinary CPython GIL/GC/no JIT/debug, isolated `-I -S -X utf8`.
Same immutable worker/host and optional 0.4 adapter; no new producer qualification.

Two independent Lython processes and CPython run sequentially per job, with
five-second warmup per role and seven rotated 150 ms rounds. Ratios are Lython /
CPython elapsed. Ranges span medians, not confidence intervals. Diagnostic/Variable
summarize role spread and IQR, not fixed design acceptance thresholds.

| Warm workload | Lython / CPython | Qualification limit |
| --- | ---: | --- |
| CSV summary/ordinary helper | 1.19× | Short diagnostic |
| Text summary/helpers | 1.61–1.63× | Remaining execution gap |
| JSON-lines/helpers | 0.98–1.01× | Variable current roles; approximately parity |
| JSON-to-Markdown report | 1.43–1.46× | Remaining execution gap |
| Configuration editing | 1.28–1.29× | Small absolute latency; fresh/compile controls variable |
| Protected file search | 1.60–1.62× | Early exit after 1,025 of 2,049 lines |
| Two-file copy/checksum/manifest | 1.58–1.60× | Two files/4,107 bytes, not large-file throughput |
| Padded numeric-field CSV | 1.02–1.03× | Explicit affected input, not ordinary CSV replacement |
| Numeric-string JSON | 1.46–1.47× | Explicit int helper; not ordinary JSON replacement |

B409's inherited raw `purpose` caption still mentions two wildcard source pairs.
The explicit 23-job list, records and contract are authoritative: this collection
contains only CPython comparisons. The separate B407 source pairs are not merged
into these ratios. The original caption is preserved rather than rewriting raw data.

[Complete timings](summary.json) include separate compile and compile-to-result
medians, role spreads, IQRs, input bytes and coarse process CPU. The authoritative
[benchmark guide](../../../docs/BENCHMARKS.md) leads with complete-script latency.
Startup, genuine pending-I/O latency, heap/RSS and embedding latency are unmeasured.
These external ratios do not isolate each change's CPU benefit or repay previous
source-pair control debt, including R51's adverse ordinary JSON observation.

Measured invocation includes mediated guest RAM-file I/O, complete stdout/file
validation and prior-result cleanup. Compilation includes construction/disposal;
compile-to-result includes fresh compilation. Fixture staging/reset, transport,
startup and final cached teardown are excluded. Efficient host overrides mediate
Lython filesystem access. CPython retains imports while Lython isolates execution
imports; this is an embedding comparison. All scripts/fixtures/goldens are shared.

Fixtures use a collection-owned 128 MiB tmpfs mount outside shared IPC storage;
actual data stay well below capacity. This changes the physical fixture path from
older `/dev/shm` collections, so do not attribute cross-run elapsed differences
solely to code. Source-pair improvements are measured separately. All source/core/
auxiliary/normal-runtime/golden/quiet/Closed/exit/join checks pass. Workers finish,
fixtures empty and the mount is removed before persistent archive verification
under the global lease. Archive is independently verified locally. 600-second
owner, 30-second stop, 30-second observation margin; no timeout restart.

Original failures are retained beside the [wildcard source-pair evidence](../2026-11-07-wildcard-translation/README.md):
B406 completed 23 CPython jobs but lost four subsequent wildcard fixture
directories; cause remains unestablished and the final quiet gate is missing.
B408 ran out of persistent space while publishing a snapshot, leaving truncated
JSON. Neither partial collection is promoted. Exact inactive old test build
outputs were offloaded/verified before retirement; future launch requires 256 MiB
persistent headroom and uses atomic snapshot publication. These corrections do
not establish the first failure's cause.

[Raw archive](observations.tgz), [receipt](archive-receipt.json), [source/image identity](identity.json),
[collection contract](contract.md), [cached terminal status](terminal-status.json)
and [historical source pairs](earlier-source-pairs.md) preserve evidence and limits.
No publication or package/tag promotion accompanies this checkpoint.
