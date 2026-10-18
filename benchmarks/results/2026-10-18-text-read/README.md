# Governed text reads: short complete-job diagnostic

Measured **2026-10-18 UTC**. This records one frozen text-reader file on
`565262377d81d8b1042341f3606911429af26322`, not an immutable build of today's HEAD.
The [frozen source identity](frozen-source.json), [patch](candidate.patch),
[preparation](preparation.json) and [validation](validation.json) identify both
workers and their inputs. No regex dependency or backend was changed.

| Engine | Median | IQR / median |
| --- | ---: | ---: |
| Earlier Lython, clean 56526237 | 11.042 ms | 2.62% |
| Lython, frozen text-read change | 10.854 ms | 1.77% |
| CPython 3.13.16 | 4.283 ms | 1.06% |

Lython / CPython is nominally **2.534×**. The candidate median is 1.71% below
earlier Lython, within sample spread. This single short diagnostic establishes
neither a speedup nor a regression. It does not measure embedding latency, peak
memory, allocation savings or genuinely suspended host I/O. No recapture or full
benchmark lane was run.

The unchanged `scripts.json-summary-helpers.large` job reads 4,096 JSON records
from 110,592 UTF-8 input bytes, calls an annotated helper, aggregates dictionaries
and writes a sorted 25-byte report, with `3072\n` on stdout. It exercises ordinary
ranged text reads, though most lines fit within one window; dedicated regressions
exercise accumulated long reads and growth refusal. The source has 607 UTF-8
bytes. Input/output identities are in [micro.json](micro.json).

The dedicated Azure Standard_F4as_v7 VM runs Ubuntu 24.04.4, Linux
6.17.0-1022-azure, four AMD EPYC 9V45 vCPUs and 16 GiB RAM without swap. Lython
uses .NET SDK 10.0.401 / runtime 10.0.12; CPython 3.13.16 uses GCC 13.3 with
PGO/LTO, ordinary GIL/GC and no JIT/debug/free-threaded build. Workers run
sequentially under the existing shared VM lease, with ordinary GC/tiering,
unbounded shared memory allowance and no instruction fuel. Worker identities
confirm no runtime overrides or BenchmarkDotNet loading. The supervisor's tiering
override is removed from its workers.

The boundary is **warm public RunAsync with completed host operations**: compiled
code is reused; invocation setup, previous-result cleanup, file reset/staging,
capture/projection and complete-file checks are timed. Compilation, startup,
transport and response hashing are excluded. The existing micro runner uses
seven interleaved measured batches per engine after warmup. Collection ownership
lasted 9.309 s, within the 15 s collection / 30 s process-group bounds.

All 35 responses / 441 invocations match stdout and complete-file goldens.
Twelve input hashes and 55 frozen baseline files are unchanged. All three workers
and fixture directories are gone; the lease is released. See [ownership](ownership.json),
[launch](launch.json), [terminal proof](terminal.json), [runner](run.py),
[raw log](micro.log) and [unaltered evidence archive](raw-evidence.tar.gz).

The change replaces accumulated CLR StringBuilder storage with the existing
GovernedByteBuilder. Capacity replacement is admitted while old storage remains
funded, and final result allocation is admitted before releasing working storage.
All six sync/async read paths release their working buffer in finally after use.
The empty RefreshOwnership/TrackEmission hooks and calls, duplicate UTF-16 window,
UTF-16 cursor traversal and final CLR-string re-encoding are removed. Existing
codec/newline/carry handling and result ownership remain. Conservative admission
may reject earlier; previous consumed windows are not transactionally rewound.

Ten small regressions fail on the clean baseline: cross-window readline(size)
exceeds Python character limits, and accumulation refuses only after consuming
the entire input. Three isolated CPython TextIOWrapper controls establish the
expected prefixes/tails. The candidate passes **428 focused Release checks**,
including UTF-8/UTF-16/newlines, before-growth refusal/cursor state, actual
suspension/cancellation, retained aliases, shared budgets, bounded batches and
complete CSV/text/JSON/file goldens. All 70 white-box JSON names/frozen records
match the preceding delivery. See [baseline proof](baseline-proof.json) and
[local validation](local-validation.json). No timeout, containment assertion or
Stress scale was relaxed; this evidence does not claim a new full Stress run.
