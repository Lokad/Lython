# Complete JSON helper job — 2026-10-17 UTC

| Engine | Measured revision | Warm RunAsync job median | IQR / median |
| --- | --- | ---: | ---: |
| Lython control A | f728636a5283dcbeb21344629802c2468c5c0525 | 12.340 ms | 1.76% |
| Lython control B | Identical worker in a separate process | 12.223 ms | 1.22% |
| CPython | 3.13.16, PGO/LTO, ordinary GIL and cyclic GC | 4.309 ms | 0.41% |

Nominal Lython / CPython: **2.837–2.864×, unqualified**. These are identical
controls, not an optimization comparison. Medians differ by 0.95%; the range
contains two medians, not a confidence interval. It does not measure later HEADs.

Existing `scripts.json-summary-helpers.large` reads 1,024 blocks / **4,096 records /
110,592 UTF-8 bytes**, calls an annotated helper, filters/aggregates amounts and
writes sorted JSON. Exact [source](source.py), [25-byte report](expected-report.json)
and [stdout](expected-stdout.txt) are retained. Independent Python aggregation
reproduces the complete goldens; fixture bytes are inside [micro.json](micro.json).

Boundary: reused compilation in warm workers, fresh invocation state, public
`RunAsync` with completed host operations versus ordinary CPython code. Timers
include setup/dispatch, previous-result cleanup, file reset/staging, execution,
capture/projection and full file checks. Compilation, startup, transport and
response hashing are outside. This measures neither genuinely suspended I/O nor
embedding latency and cannot establish a change from historical fresh-run data.

Dedicated Azure Standard_F4as_v7: Ubuntu 24.04.4 LTS, Linux 6.17.0-1022-azure,
four AMD EPYC 9V45 vCPUs, approximately 16 GiB RAM, no swap. Clean Release source
build: SDK 10.0.401 / runtime 10.0.12. CPython uses GCC 13.3.0, PGO/LTO, no
JIT/debug/free-threaded mode, with `-I -S -X utf8`. Live [preflight](preflight.json),
[preparation](preparation.json), [build](build-subject.log) and loaded identities
in the raw capture identify the actual inputs.

Lython keeps ordinary limits/checks and explicit ownership under an unbounded
shared allowance per worker. Final results survive response writing and cached
compilations are disposed at exit. There is no denied admission, finite-budget
or RSS qualification. CPython keeps ordinary GC without equivalent in-process
containment. Worker runtime overrides are null: ordinary GC/tiering remain.
The launcher removes the supervisor's tiering override. No costs are subtracted.

One existing micro capture used one-second worker warmups, two-second settling
and seven rotating-order 25 ms batches. Collection deadline: 15 seconds; owned
systemd group cap: 30 seconds, under the shared lease. Total: **9.495 seconds**.
All **37 responses / 466 invocations** match full stdout/file goldens and digests.
Eight distinct timing inputs are unchanged, source clean, all workers/fixture
directories gone, service inactive with an empty group, lease reusable. See
[validation](validation.json), [ownership](ownership.json), [launch](launch.json),
[terminal](terminal.json), [cleanup](final-cleanup.json) and
[raw archive](raw-evidence.tar.gz). No recapture, long lane or framework change.

The separate [shared-defaults candidate](../2026-10-17-json-default-options/README.md)
has its own changed source, allocation evidence and paired capture. Do not pool
samples or infer a trend across the two captures.
