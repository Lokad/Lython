# CSV pull consolidation — 2026-10-17 UTC

| Engine | Measured source | Warm complete-job median | IQR / median |
| --- | --- | ---: | ---: |
| Lython | One frozen CSV runtime file on 1b6a1531 | 10.194 ms | 2.43% |
| CPython | 3.13.16, PGO/LTO, ordinary GIL and cyclic GC | 3.783 ms | 0.54% |

The nominal Lython / CPython ratio is **2.694×**, unqualified. Earlier Lython
1b6a15318b00ec056af02e23b72fa64f861964df measured 10.007 ms with 4.31% spread.
The candidate median is **1.87% higher**, within observed spread: no whole-job
improvement or regression is established. This single diagnostic does not measure
later HEADs or predict a benefit from the code deletion.

The change folds the single-use `CsvRecordSource.PullAsync` body into
`TryMoveNextAsync` and deletes the former method. It removes one async forwarding
state-machine entry per acquired fragment, adding no mode, abstraction, owner,
cache or policy. Ready-row checks, live indexed advancement, awaited movement,
feed/error handling and terminal cleanup retain their order. Synchronous pull,
parser and ownership implementations are unchanged. The simplification is retained
without an elapsed-time improvement claim; no allocation saving was measured.

Workload: existing `scripts.csv-summary-helpers.large`, the unchanged
[annotated-helper script](../../scripts/csv-summary-helpers.py), 1,024 blocks /
4,096 CSV records / 57,361 UTF-8 input bytes. It includes quoted newlines and
non-ASCII text, calls the helper per row, filters/aggregates amounts, writes the
complete sorted report and prints `3072\n`. The fixture and complete goldens are
identical to [BV](../2026-10-17-csv-helper-warm-async/README.md).

Boundary: warm reused compilation with fresh guest invocation state. Lython calls
public `RunAsync` against completed host operations; CPython runs ordinary code.
Both timers include prior-result cleanup, file staging/reset, complete execution,
capture/projection and full output-file checks. Compilation, startup, transport
and response hashing are outside. This is not genuine suspended-I/O throughput
or an embedding-latency measurement. Do not infer a change from other boundaries.

Both Lython workers use ordinary public limits and an unbounded shared governed
allowance, with explicit result/cache cleanup. This run has no admission refusal
and does not qualify finite-budget memory usage. CPython retains ordinary GIL,
reference counting and cyclic GC without equivalent in-process containment.
Nothing is subtracted. The supervisor's tiering override is removed when workers
launch; all recorded worker overrides are null. BenchmarkDotNet is not loaded.

Machine: dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS, Linux
6.17.0-1022-azure, four AMD EPYC 9V45 vCPUs, approximately 16 GiB RAM, no swap.
Both Release builds use SDK 10.0.401 / runtime 10.0.12. CPython 3.13.16 is the
GCC 13.3.0 PGO/LTO build without JIT, debug or free-threaded mode, launched with
`-I -S -X utf8`. Binary/runtime identities are in [micro.json](micro.json).

The candidate is explicitly a working source snapshot, not an immutable candidate
build. Its base is 1b6a1531; the only modified runtime file is
`LythonRuntime.CsvModule.cs`, canonical SHA256
`ddc0a48a4d13177580f767f0276e7378b5fbb8feb4af022036298aa6f1947f1c`.
The exact [file](candidate-CsvModule.cs), [patch](candidate.patch),
[source declaration](frozen-source.json), [preparation](preparation-from-origin.json)
and [build log](build-origin.log) are preserved. The validated source matches the
file delivered with this evidence; assembly metadata still names the base.

One existing fifteen-second `--compare micro --lane warm-async` collection ran
under the shared lease and a thirty-second owned systemd group cap, with one-second
warmup per worker, two-second settling and seven rotating-order 25 ms batches.
The complete owned run succeeded in **9.419 seconds**. All **36 responses / 510
invocations** match source/fixture and complete stdout/file goldens. All twelve
timing inputs are unchanged. All three workers and fixture directories are gone,
the service is inactive with an empty group, and the lease is released. See
[validation](validation.json), [ownership](ownership.json), [launch](launch.json)
and [terminal cleanup](terminal.json). No timing recapture or long lane ran.

Preparation had two failures: an SSH
payload initially exceeded the local command-line limit before SSH started, and
a local clone of the partial checkout failed before building. A fresh sparse
checkout then built successfully. These are staging failures, not runtime or
measurement results; neither started a timing worker. The local SSH failure
receipt remains in the private preparation notes; the remote clone failure is in
the [raw archive](raw-evidence.tar.gz).

The preceding ten-second [CPU profile](profile.json) measures immutable **1b6a1531
before this change**, not before/after CPU improvement. All worker threads are
included: 7,982 samples, zero lost events/missing stacks, 3.47% unresolved leaves.
CSV dictionary reading appears in 26.81% inclusive stacks, record-source pulls
19.57%, parser Feed 11.24%; these overlap. Main interpreter/Feed leaves are
13.78%/3.73%. Ordinary JIT work contributes 13.14% inclusive samples. Inlining and
tiering limit causal interpretation; these percentages do not predict savings.

The profile's 517 responses / 1,032 invocations preserve goldens; all 546 source
hashes match immutable Git blobs, binary identities match BV, and cleanup succeeded
in 14.618 seconds including startup. The existing [runner](profile-runner.py) and
[full protocol records](profile-responses.jsonl.gz) are retained. Raw trace SHA256
is recorded in profile.json; the 20 MB trace and original receipts remain in the
private profiling archive. GC records do not qualify embedding pause latency.

Local Release checks pass: 415 focused semantic, storage/lifetime, shared-budget,
admission, delayed-I/O/cancellation, stack and complete-file-golden controls.
The 156 selected baseline test names/outcomes match exactly. Source verification
confirms only the helper inlining; no test scales, limits or assertions changed.
Full Stress was not rerun. Cross-platform/package qualification is reported with
the runtime delivery, separately from this diagnostic evidence.
