# Lython and CPython comparison contract

Specification version **5**, established before the targeted core-loop run. The catalog
and explicit correctness check are available. Persistent worker primitives are
available, including supervised correctness/timer smokes, paired collection,
machine checks and raw-evidence report rendering.
The [2026-10-09 bounded run](results/2026-10-09-quick/README.md) completed all four
lanes within ten minutes each. Its raw-evidence review found no qualified
workload ratios; a qualified baseline remains pending.
The [focused basic-loop investigation](results/2026-10-09-core-loop/README.md)
completed in 2m13s. Its separate CPU profile identifies interpreter dispatch
as the leading design investigation; strict stability checks still withhold
qualified comparative multipliers.

The [first synchronous dispatch improvement](results/2026-10-09-sync-dispatch/README.md)
used two short collections of about seven seconds each. Their diagnostic medians
show 13.4–14.0% less time on the 16,384-iteration loop, with correctness checks
passing and milestone qualification deferred.

Ordinary improvement rounds use the short `micro` command below. Full lanes
are reserved for occasional declared milestones, with their existing ten-minute
cap and qualification rules. Microbenchmarks provide diagnostic feedback in
seconds; they do not run idle gates, establish a qualified speedup or replace
correctness checks.

## Short improvement rounds

Build the old and candidate benchmark workers in Release before timing. The
short command reuses one identical precompiled case in old Lython, candidate
Lython and isolated CPython, with fresh state, ordinary limits/GC, complete
golden-output checks, one second of warmup per worker, a two-second settling
pause and seven rotating-order batches calibrated to 25 ms. It records every
response, loaded worker identity and before/after input hashes. Its table reports
per-job medians and interpolated IQR/median; no confidence interval or certified
multiplier is claimed. Compilation, transport and output hashing stay outside
the worker timer, as in the warm lane.

```text
<dotnet> <candidate-benchmark.dll> --compare micro --catalog <catalog.json>
  --dotnet <absolute-dotnet> --baseline-worker <old-benchmark.dll>
  --python <absolute-python> --python-worker <cpython-worker.py>
  --out <new-micro.json> [--case <id>]
```

The default case is `loops.integer.large`. Select an existing case explicitly
when testing another hypothesis. These jobs should last seconds. Collection has
a 15-second deadline; allow another 15 seconds for owned-worker cleanup. On the
VM, enforce the complete **30-second hard limit** with a dedicated systemd service
using `RuntimeMaxSec=30`, `TimeoutStopSec=0` and `KillMode=control-group`.
[run-micro-linux.sh](run-micro-linux.sh) supplies the shared VM lease and the
core-loop manifest. It takes absolute dotnet/Python paths, the old worker DLL,
a new evidence directory, the lease path and an optional case ID. The supervisor
alone disables its tiered compilation; every worker removes that override.
Build, test and transfer beforehand; preserve partial/failed receipts. Never
overwrite a receipt or automatically repeat a noisy diagnostic to pick a winner.

The primary run has a hard **10-minute wall budget per lane**, including
preparation, verification, noise waits, retries and worker cleanup. Build and
install toolchains beforehand; render reports offline afterward. Four lanes
therefore consume at most **40 minutes** of VM collection. Policy v1's full
131-case run was stopped because its sampling schedule took hours per lane;
its partial evidence is retained separately and is not mixed with the current profile.

The quick profile selects **12 workloads plus two controls** before timing:
integer loops at medium/large scale, keyword calls, stable sort, tuple-key
dictionaries, generator drain, supplementary Unicode scanning, ASCII text
pipeline, JSON roundtrip, retained CSV rows, XML selection and zlib. The full
131-case catalog remains a correctness suite; this smaller baseline cannot
establish broad scaling or coverage of every supported library.

The `core-loop` profile zooms into one basic integer reduction at 256, 2,048 and
16,384 iterations, plus the empty/tiny controls. It uses the existing identical
source and independent scalar checksum goldens, so output remains small across
sizes. Public invocation overhead remains timed. This profile is intended to
separate fixed entry cost from scaling, before profiling the implementation.

The primary question is how long the same supported Python job takes through a
warm public Lython invocation and a warm CPython invocation on the same machine.
The existing BenchmarkDotNet classes remain useful for managed allocation and
local regression diagnostics. They do not provide a CPython comparison.

## Reference profile

The first reference is independently built **CPython 3.13.16**, Linux x86_64,
ordinary GIL-enabled Release, with **PGO and LTO**, no debug build and no
experimental JIT. Both engines run on the same dedicated Linux VM, initially
with its normal four-core topology and ordinary GC behavior. Native library
measurements record their underlying implementations and versions separately.

The .NET profile is **SDK 10.0.401 / Microsoft.NETCore.App 10.0.12**, ordinary
Release builds. The repository's global.json can roll forward; the driver must
verify the effective SDK, runtime and loaded binaries instead of trusting that
request. Record .NET GC mode, tiering/PGO, environment overrides and visible CPU
count. Do not force collections or disable CPython cyclic GC during timing.

`prepare-reference-linux.sh` is an explicit, untimed setup command. It downloads
these pinned archives, verifies their published hashes, installs under the
chosen toolchain directory and writes a receipt containing actual executable
hashes, build configuration, flags, versions and native library identities.
It does not modify shell profiles or install third-party Python packages.

```bash
bash benchmarks/prepare-reference-linux.sh --install-build-dependencies
```

The optional flag installs compiler/library prerequisites using passwordless
sudo. Omit it when dependencies are already installed. Use `--root /absolute/path`
to select a different toolchain directory. The default is
`$HOME/lython-bench-toolchains`; the pinned Python executable is
`cpython/3.13.16/bin/python3.13` and the SDK launcher is `dotnet/10.0.401/dotnet`
under that directory. Installation/build logs and receipts are local artifacts.

- Python source archive SHA-256:
  `f4b1bfb3c79b5bb11b8d228a12504163b4c0dab4d679828d8f5f26b6cb6ab35d`.
  [Official release and checksums](https://www.python.org/downloads/release/python-31316/).
- SDK archive SHA-512:
  `51c8b999af9e8dd9998c9edc5944e19a90788862068acd38694e098889054ce8c23d4f0c5cccfa16bf187d044562359e5ee69a9f8ad0bbe913ba90311fbce25b`.
  [Microsoft release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json).
- Python's optimized-build guidance recommends PGO and LTO; the preparation
  script retains the ordinary interpreter configuration around those options.
  [Python build options](https://docs.python.org/3.13/using/configure.html#performance-options).

Launch the explicitly supplied Python executable with **`-I -S`**. Record its
effective isolation/no-site flags, paths and build properties. Isolated mode
ignores PYTHON environment variables, including PYTHONHASHSEED. Input generation
uses a fixed fixture seed; the primary profile does not claim a fixed runtime
hash seed. At least three independent worker sessions expose hash/layout and
startup variability. A controlled hash-seed profile would be a separate campaign.
[Python isolation](https://docs.python.org/3.13/using/cmdline.html#cmdoption-I).

## Timed lanes

| Lane | Lython | CPython | Boundary |
| --- | --- | --- | --- |
| Warm invocation, primary | Reuse `LythonCompiledScript`, public synchronous `Run` | Reuse `compile(..., 'exec')`, `exec` into a fresh namespace | Per-invocation setup, shared script, bounded capture, result extraction and cheap success/output verification |
| Compile and run, secondary | Compile and invoke on a warm worker | Compile and execute on a warm worker | Both compilation and the complete invocation boundary |
| Compilation, diagnostic | Successful `LythonEngine.Compile` | Successful `compile` | Compilation, validity check and consumption of the compiled object; execute during untimed validation |
| Fresh process, secondary | Launch .NET worker, load library, compile and run once | Launch Python worker, compile and execute once | Parent-observed launch through complete result/stderr drain and process exit, for both engines |

Warm lanes use persistent workers and clocks inside those workers. Source/input
transport, framing, JSON artifact serialization and the full semantic comparator
stay outside timing. Lython's public global ingress, contained execution and
public output/return projection stay inside. Its dedicated-stack execution
machinery is part of that public invocation. CPython's fresh namespace and text
capture setup stay inside too. These are different implementations of the
declared boundary, not a measurement of instruction dispatch alone.

Use the same ordinary Python source with bounded `print` output in both engines;
do not use Lython's top-level return extension. Immutable fixture templates and
compiled code may be reused. Copy mutable input state and create fresh globals
on every invocation inside the timer. Do not reuse modified lists/dictionaries,
memoized results or a exhausted iterator. Import statements remain at their
shared source positions; both runtimes may retain natural module caches.
Do not purge CPython's sys.modules or impose artificial containment on it.

Full outputs are semantically validated before and after warmup, with independent
expected values where practical. Every measured invocation checks success and
complete bounded output against the validated lane-specific expected output.
Perform those cheap checks inside each timer and record completed invocations.
Compilation-only batches retain/consume their successful compiled result.
Output differences in deliberately formatting-sensitive jobs are mismatches;
do not normalize away type, precision, order or encoding errors.

Fresh-process runs naturally reuse OS file caches. They do not claim cold storage.
Keep startup, compilation and warm tables separate. Empty/tiny controls expose
overhead and are never subtracted from another result. Async execution would be
a separately declared later profile; Python async syntax is outside this campaign.

## Limits and correctness

Keep ordinary Lython memory, projection, collection, stack, host-call and output
limits enabled. Leave optional instruction fuel unset. The benchmark host grants
no filesystem, network, subprocess, stdin or local-import capabilities. Capturing
bounded printed output does not introduce an ambient runtime capability.

CPython has no equivalent in-process governor. Disclose that distinction; the
same external finite watchdog bounds both workers without claiming equivalent
containment. The driver must kill and reap only its own worker processes on
protocol failure or timeout, and preserve completed checkpoint rows.

Classify every case before timing as **Equivalent**, **Unsupported**, **Mismatch**,
**BudgetDenied** or **Failure**. Errors and denials never count as fast results.
Validate complete values, integer/boolean types, counts, order and materialization.
Preserve exact integer precision. Any floating tolerance must be declared in the
case specification before timing; formatted text and bytes require exact equality.

The initial catalog covers loops/arithmetic, calls/classes/closures, lists/tuples,
dictionaries/sets, comprehensions/generators/itertools, ASCII/BMP/supplementary
strings, JSON, CSV with memory streams, collections and small library pipelines.
Compression is a separate library category. Include random Unicode indexing,
retaining/discarding contrasts and controls, with family-specific geometric
small/medium/large scales that fit normal budgets. Freeze actual source/fixture
digests and size manifests after correctness checks, before viewing timing ratios.

Regex waits for the pending Utf8Regex integration qualification. Third-party
packages, OpenPyXL and real filesystem/network/subprocess jobs are excluded from
this first comparison. Later I/O work needs equivalent storage/host paths.

## Candidate catalog and correctness checks

Export the finite catalog without running a benchmark:

```bash
dotnet run -c Release --project benchmarks/Lokad.Lython.Benchmarks -- --compare list --out /absolute/path/catalog.json
/absolute/path/python3.13 -I -S benchmarks/verify-catalog.py --catalog /absolute/path/catalog.json --out /absolute/path/cpython-correctness.json
```

Omit `--out` to list identifiers, categories and sizes. The export contains 131
candidates: two invocation controls and 43 workload shapes at three scales.
All sources are ordinary shared Python scripts. Immutable `N`/`TEXT` fixtures
are rendered as literals before the shared job; mutable containers, functions,
classes, streams and generators are created anew during each invocation. The
compiled scripts may be reused, as specified for the warm lane.

| Scale family | Small / medium / large | Distribution and consumption |
| --- | --- | --- |
| Core elements | 256 / 2,048 / 16,384 | Explicit ranges/modular values, deterministic stable-sort ties and tuple-key collisions; all requested values consumed, except the named generator early-exit job (N/8) |
| Text blocks | 128 / 1,024 / 8,192 | Repeated ASCII/BMP/supplementary blocks; scans consume all code points, indexing performs 512 deterministic requests regardless of text size |
| Library records | 16 / 128 / 1,024 | Ordered JSON/CSV/XML records and struct packets; CSV retaining/discarding jobs process all rows, early exit processes N/8 |
| Compression | 128 / 1,024 / 8,192 blocks | Gzip repeats a 16-character block; zlib uses deterministic xorshift text seeded with 0x5EED1234; complete decompressed bytes are checked |

Each candidate records its exact source, fixture JSON and independently computed
expected stdout, their SHA-256 digests, source/output UTF-8 byte counts and text
fixture byte/code-point counts. Wide integer reductions exceed 2^53; floating
jobs use exact dyadic values and exact text, with no tolerance or lossy numeric
normalization. Golden values are computed by independent C# algorithms rather
than copied from either interpreter.

The public test suite executes every candidate twice synchronously and twice
asynchronously with fresh ordinary public invocations. The explicit Python
checker verifies manifest hashes, compiles once and executes twice into fresh
namespaces, checking complete stdout and empty stderr against the same goldens.
Its atomic receipt records the catalog, checker and actual interpreter hashes,
flags/build configuration and every case outcome. Both checks are untimed;
their success establishes correctness, not timing eligibility. The Python
checker runs trusted fixtures and should have an external finite watchdog.

Transport must preserve explicit UTF-8 bytes and verify received source/fixture
hashes. Agreement between interpreters alone is insufficient: if both receive
the same corrupt source, only an independent expected output catches it.

## Worker protocol

The .NET entry point `--compare worker --catalog <catalog.json>` and the maintained
`cpython-worker.py --catalog <catalog.json>` implement the same persistent protocol.
Launch Python explicitly with `-I -S`. These are internal supervised worker entry
points for trusted fixtures; an external process watchdog is required. They do
not yet constitute a qualification command or a fresh-process measurement lane.

Protocol version 1 uses a four-byte big-endian byte length followed by strict
UTF-8 JSON, capped at 4 MiB per frame. Catalog files are capped at 64 MiB and
256 cases. Startup validates every source/fixture/golden digest and emits a Ready
frame with the catalog hash and actual runtime/binary/clock identities. Requests
have increasing positive 32-bit IDs, a protocol version and an operation:

- `verify`: case ID and all three hashes; compile once and check complete output
  twice with fresh invocation state. Only an Equivalent result admits batches.
- `batch`: the same identity, lane (`warm`, `compile-run` or `compile`) and a
  positive count capped at 1,000,000. Internal monotonic clocks exclude protocol
  traffic, output hashing and response encoding. Counts and ticks/frequency are
  returned for successfully completed batches. Mismatch/denial/failure retains
  completed counts for diagnostics, invalidates the case and returns no elapsed
  sample.
- `quit`: acknowledge Closed and exit. Clean EOF differs from a truncated frame.

The internal 60-second batch ceiling is checked between jobs. A stuck individual
job or stalled pipe still requires the external watchdog, which will be provided
by the driver. Loop/count/deadline checks are included in batch time and visible
through invocation controls. Python's adapter bounds each captured stream at
16 MiB; this capture cap does not provide a guest memory/work governor. Neither
adapter disables GC or adds optional Lython instruction fuel. Worker startup
provenance hashing is outside all persistent timed lanes.

The ordinary public suite covers namespace reset/code reuse, Unicode, malformed
or truncated frames, wrong hashes, replayed IDs, count ceilings, unsupported
syntax and wrong output of equal length. These checks have no separately installed
Python or timing-performance threshold dependency. Controlled subprocess fixtures
also exercise deadlines, cancellation, stderr flooding, invalid replies, nonzero
exits, and descendants holding pipes after the worker root exits.

## Supervised correctness and timer smoke

Build Release and export the catalog first. Supply both interpreter executables
explicitly; the supervisor does not install or search for a reference interpreter:

```text
<dotnet> <benchmark.dll> --compare verify --catalog <catalog.json> --dotnet <absolute-dotnet> --python <absolute-python> --python-worker <cpython-worker.py> --out <receipt.json>
```

This command checks every case twice before and after two-invocation diagnostic
batches in the warm, compile-run and compile lanes. It validates the catalog,
loaded adapter/library/runtime digests, CPython executable/helper digests, normal
GC/GIL mode, reply sequence, case/input/output hashes, counts and clocks. It checks
complete clean shutdown and writes an atomic receipt after each case and on
failure/interruption. Receipts always have `performanceQualified: false`; there
is no warmup, quietness gate, sampling qualification or speedup report yet.

One 10-minute campaign deadline contains common 30-second startup, 65-second
request, 5-second shutdown and 5-minute case deadlines. Writes, reads and validation share each
request deadline. Stdout frames are capped at 4 MiB with at most one expected
reply queued; stderr is drained concurrently and capped at 64 KiB. Malformed,
unsolicited, truncated, inconsistent or failed timed results cannot become samples.
Cancellation and errors stop owned processes and join the I/O operations/pumps.

Windows workers enter a job atomically at creation, inherit only the three pipe
handles, and have no breakaway flag. Linux uses `/usr/bin/setsid --fork --wait`
and a fixed `/bin/sh` bootstrap: a private gate holds the session leader until
the parent checks its PID, session/group and parent relationship, then executes
the explicit worker with positional arguments. Both approaches can stop owned
descendants even after the root exits. The Windows job is checked empty; the
Linux launcher is reaped, and killed orphan descendants may briefly await PID1
reaping. Cleanup never searches process names or terminates unrelated processes.
These are lifecycle controls for trusted fixtures; they do not provide a guest
security boundary against a process deliberately escaping its Linux session.
macOS process ownership is currently unsupported.

The Windows mechanism follows Microsoft's [job object documentation](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects)
and [process attribute API](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-updateprocthreadattribute).
The Linux launcher uses the documented [`setsid` fork/wait options](https://man7.org/linux/man-pages/man1/setsid.1.html).
Qualification and reporting apply additional raw-evidence eligibility checks.

## Fresh-process correctness and timer smoke

The dedicated .NET `--compare once --catalog <one-case.json>` entry and Python
`-I -S cpython-worker.py --once --catalog <one-case.json>` compile and execute
exactly one job, write one framed result and exit. They reject multiple-case
payloads. They keep the same ordinary limits/capture, fresh namespace, complete
golden output check and success/return/stderr checks as the persistent lanes.
Each result carries lightweight actual runtime/build identity; full binary
hashing and heavyweight Python provenance imports stay outside fresh startup.
The .NET comparison branch does not initialize BenchmarkDotNet.

```text
<dotnet> <benchmark.dll> --compare smoke-fresh --catalog <catalog.json> --dotnet <absolute-dotnet> --python <absolute-python> --python-worker <cpython-worker.py> --out <receipt.json>
```

This diagnostic command prepares and retains byte-identical one-case payloads
before timing. Persistent workers first provide full binary/runtime provenance
and two untimed semantic checks per case, then shut down. Each selected case
gets a new Lython process and a new CPython process, sequentially. The same
parent clock starts before the owned OS launch and stops after complete raw
stdout/stderr drain and root exit. It includes the common ownership bootstrap,
runtime/adapter startup, payload reading/parsing/hash checks, compilation, one
invocation, cheap result checks, result encoding and pipe transport. Parent
decoding, semantic/identity validation, lifecycle cleanup, full binary hashes
and receipt writes are outside that clock. Payload/executable/helper/library
identities are bound to the prepared files and rechecked outside samples.

Only one successful invocation with matching case/fixture/source/output hashes,
runtime/build identity, a single complete frame, empty stderr and zero exit
can retain a parent elapsed value. Failure responses have no parent timing
sample; malformed counts/frames, stale identities and unexpected exits abort
and checkpoint. Stdout/stderr and owned-process cleanup use the same bounds as
the persistent supervisor. This smoke has a 65-second launch-to-drain ceiling,
5-minute case and 10-minute campaign ceilings. It records every completed row
atomically and retains the prepared payload directory beside the receipt.

OS file caches are retained. The common Windows job or Linux gated-session
envelope is included, and its relative cost is visible in the controls. There
is no overhead subtraction and no claim of cold storage. Results remain
`performanceQualified: false`: one process per engine/case is correctness/timer
smoke, not repeated-session sampling or a qualified startup ratio. Fresh,
compilation and warm results remain separate. Qualification requires the paired
collection and eligibility checks below.

The qualification **supervisor alone** is launched with
`DOTNET_TieredCompilation=0`. A bounded thread-CPU diagnostic found its background
tiered compiler using 240 ms during a failed half-second idle check; the corrected all-thread trace confirmed supervisor compiler activity and
separately found worker compiler activity after short warmup. Both persistent and exactly-once worker
launches remove this one inherited variable, so Lython retains normal tiering/PGO
and CPython retains its normal GC/GIL. Other overrides remain visible and reject
qualification. The receipt records and validates the supervisor override set,
binds it on resumption, and distinguishes it from the workers' default profiles.
Microsoft documents this [compilation setting](https://learn.microsoft.com/en-us/dotnet/core/runtime-config/compilation).

Policy v4 restored one-second/32-invocation warmup for the persistent
lanes after a corrected process/thread diagnostic observed worker background
compilation with 100 ms warmup. Fresh-process preparation remains eight once
jobs and at least 100 ms; fresh children cannot retain JIT state across jobs.
The selected jobs, seven-pair sampling, statistical thresholds and 600-second
lane budget are unchanged. Failed v2/v3 diagnostic evidence is retained separately.

A bounded all-thread diagnostic under v4 still observed a worker tiered compiler
using 60 ms CPU during a failed first idle window. Policy v5 therefore records a
fixed **two-second preparation pause after calibration and before sampling**.
That pause is inside the original case/lane deadlines and applies equally before
paired collection. It neither disables worker compilation nor retries a failed
gate. Parent-clock evidence must prove its duration. The noise, stability,
overhead and sampling thresholds remain unchanged; earlier receipts retain their
original exclusions and must be rendered with their original revision.

## Sampling and eligibility

The measurement policy is fixed before timing:

- One half-second idle CPU window: at most **3% median / 5% maximum** background
  activity. Settle and recheck between every pair. Unsupported accounting cannot
  qualify; an affected case is excluded without rerunning it. Record steal time, throttling, paging
  and memory pressure where observable. No tests, builds or other campaigns run
  on the VM during collection.
- Warm persistent workers for at least **32 successful invocations and one second**.
  Fresh-process preparation uses **8 exactly-once jobs and 100 ms**. Bind the
  recorded lane to its warmup evidence; fresh preparation cannot qualify a
  persistent session.
  Keep normal .NET tiering and CPython specialization. Calibrate the lanes
  independently, using two confirming batches of at least **10 ms**; retain only
  measured batches lasting at least **5 ms**. Record counts and ceilings.
- **7 sequential paired batches**, balanced alternating AB/BA, in at least
  **three independent worker sessions**. Balance starting order between sessions.
  Retain all samples and session identities, including slow or failed attempts.
- Record absolute per-invocation medians. Ratio is CPython time / Lython time.
  Use a fixed-seed paired-bootstrap **95% interval** for the median log ratio
  within each session. Show between-session variation rather than pooling
  correlated batches as independent workers. An interval crossing parity names
  no winner.
- Mark Unqualified when either lane's IQR/median exceeds **10%**, AB/BA order
  changes the ratio by more than **10%**, or the ratio interval spans more than
  **15%** multiplicatively. The largest/smallest independent-session median ratio
  must also be at most **1.10**.
  Do not weaken thresholds to obtain publishable results. Controls and jobs
  dominated by harness overhead remain visible without a speedup claim.

Policy/eligibility v5 freezes finite invocation/batch/case/campaign deadlines,
iteration ceilings, case order and protocol size caps in its versioned manifest
before measurements. A ceiling or interrupted campaign does not relax eligibility.
Do not force GC or inherit timeit's default cyclic-GC suppression.
[Python timeit policy](https://docs.python.org/3.13/library/timeit.html#timeit.Timer.timeit).

## Collecting and rendering a campaign

The opt-in commands require explicit absolute executables and the receipt from
`prepare-reference-linux.sh`. Run from a clean committed repository root using
the matching Release build. Initial qualification supports the four-core Linux
x86_64 profile above; correctness smokes remain available on Windows and Linux.

```text
<dotnet> <benchmark.dll> --compare check-machine --out <machine.json>
<dotnet> <benchmark.dll> --compare list --profile quick --out <catalog.json>
env DOTNET_TieredCompilation=0 <dotnet> <benchmark.dll> --compare qualify --catalog <catalog.json> --dotnet <absolute-dotnet> --python <absolute-python> --python-worker <cpython-worker.py> --toolchains <toolchains.json> --out <receipt.json> --case quick --lane warm
<dotnet> <benchmark.dll> --compare render-report --receipt <receipt.json> --out <report.md>
```

Use `--compare list` to obtain all correctness case IDs, `--profile quick`
for the fixed 14-case timing manifest, or `--profile core-loop` for the five-case
loop profile. Qualification selects it with `--case core-loop`. `--case quick` is the default;
explicit `all` or comma-separated selections retain the same ten-minute ceiling.
`--lane` accepts `warm` (default), `compile-run`, `compile` and `fresh-process`.
The empty and tiny controls always precede selected cases. Each case gets three
independent sessions, seven pairs per session, and a reversed 4/3 starting-order
split in the middle session. Each lane calibrates its own fixed iteration count.
Post-warmup verification reuses the compiled code, preserving specialization.
Fresh batches sum parent launch-to-drain times for independent exactly-once
processes; their individual identities/results remain in the receipt.

Policy/eligibility v5 uses 10,000 intact-pair bootstrap resamples with seed 1729,
explicit xorshift32/rejection-index sampling and linear `(n-1)*p` quantiles.
It retains session medians and intervals separately. No aggregate interval is
computed. The common invocation ceiling is 1,000,000; warmup is capped at twelve
batches and calibration at eight. Worker deadlines are 30 seconds at startup,
65 per request/parent batch and five at shutdown, further bounded by the remaining
case/lane time; each case has **45 seconds** and each lane has **600 seconds**.
Ten seconds are reserved for cleanup. The original start time is retained on
resumption, and over-budget evidence cannot qualify. Receipt reads are capped at 512 MiB to retain the full
catalog's gates and individual fresh-process observations within a finite bound.

To exclude jobs dominated by invocation overhead, each lane's per-job median
must exceed ten times the larger of that session's two control medians. Controls
must themselves pass the timing/quietness checks, and receive absolute times
without ratio claims. This rule is frozen before the first timing campaign.

The Linux gate retains aggregate CPU counters, paging counters, memory-pressure
totals and the current cgroup plus ancestor throttling counters. Any observed
steal, paging, pressure or available throttling increment stops collection.
Unavailable optional counters are explicitly null; CPU/paging accounting is
mandatory. Counter regression or changing availability cannot qualify. Kernel
references: [CPU accounting](https://docs.kernel.org/filesystems/proc.html),
[pressure totals](https://docs.kernel.org/accounting/psi.html) and
[cgroup throttling](https://docs.kernel.org/admin-guide/cgroup-v2.html).

`run-comparison-linux.sh` runs one lane with at most three attempts and a
five-second wait after initial/final noise. The original wall deadline includes
all attempts. Run it in a dedicated systemd service with `RuntimeMaxSec=600`,
`TimeoutStopSec=0` and `KillMode=control-group`; this hard limit also covers
stuck processes and descendants. Supply absolute .NET/Python/toolchain paths,
an output directory, a common VM lock file and the lane name as positional
arguments; an optional seventh argument selects `quick` (default) or `core-loop`.
Collect lanes sequentially, with no builds/tests/transfers during
timing, then render receipts offline. Do not extend the budget after a timeout.

Runtime specialization can still be in progress, and seven pairs
provide less statistical evidence. Stability, semantic and overhead thresholds
are retained; the smaller run may legitimately produce more exclusions.

The collector checkpoints raw warmup, calibration, gates, pair order, every
completed/failed batch, correctness checks and provenance atomically. Exit 3
means initial/final noise stopped the attempt; exit 1 means failure/interruption, and exit 2
means invalid arguments. A completed collection can contain Unqualified rows.
Only fully eligible rows expose ratios. The renderer recalculates from raw
counters, responses and samples; stored eligibility/statistics flags are advisory.
Partial or changed evidence produces a clearly labeled diagnostic report without
qualified ratios or winners.

Fresh-process reporting verifies each job's parent clock, once-request ID,
output and payload identity, distinct positive process IDs within each batch,
and the exact sum of individual launch-to-drain times. Runtime identities are
compared as JSON values, so saving an indented receipt preserves equivalent
options while changed runtime options still invalidate the sample.

An existing receipt requires `--resume true`. Resumption compares policy, source,
machine, catalog/case order, toolchains and every input/build digest. It archives
the previous complete attempt beside the receipt, preserves finished
measurements and exclusions, and restarts partial sessions. An excluded row is
not rerun to improve eligibility after a later noise stop. Schema/protocol/policy
versions, requested scope and effective runtime configuration must also match.
Changed identities require a separate campaign.

## Evidence and delivery

Atomic versioned JSON checkpoints contain clean source revision; loaded assembly,
Python, adapter and dependency identities; VM/OS/CPU/storage/timer information;
effective runtime options; source/input/output digests; correctness, warmup,
calibration and CPU checks; every paired sample; session/order identities; and
failures or exclusions. Stale or partial evidence cannot produce a completed
qualification report. Resumption requires unchanged source/environment identities.

Generate concise maintained reports from those exact checkpoints. Keep raw logs,
traces, samples, toolchain installations and SSH details out of Git and NuGet.
Report each family/scale/lane with absolute times and qualified ratios/intervals,
including weaknesses and every excluded row. There is no overall "Lython is X
times faster than Python" figure from this selected catalog.

Allocation diagnostics stay separate: managed allocation, governor peaks and
tracemalloc have different coverage. A memory comparison needs the same OS
process metric and invocation count in separate diagnostic runs. Review profiles
and propose optimizations only after the first baseline is recorded.

Ordinary CI verifies harness correctness and package exclusion, with no timing
thresholds, benchmark campaigns or hidden interpreter download. Real CPython
verification remains explicit with a supplied executable; controlled test workers
cover protocol errors, timeouts, malformed evidence and cleanup.
