# Ordinary JSON loads — 2026-10-17 UTC

| Engine | Measured source | Warm RunAsync job median | IQR / median |
| --- | --- | ---: | ---: |
| Lython | One frozen JSON module file on 6c9a51df | 11.631 ms | 1.67% |
| CPython | 3.13.16, PGO/LTO, ordinary GIL and cyclic GC | 4.245 ms | 0.83% |

Nominal Lython / CPython: **2.740×, unqualified**. Earlier Lython 6c9a51df
measured 11.893 ms with 1.82% spread. The candidate median is nominally 2.2%
lower. This is one short diagnostic, **not a qualified speedup** or embedding-
latency result. Earlier [numeric scratch measurements](../2026-10-17-json-number-scratch/README.md)
are separate data and cannot establish a trend across captures.

The existing native JSON callable recognizes only the built-in `loads` signature
with one positional argument. That complete shape needs no generic eight-slot
binding array or extra-keyword scan. Shared input validation and immutable default
options enter the same parser directly. This also omits two forwarding state
entries on the async route. All three existing checkpoints before parsing remain.
Keywords, explicit options, custom decoders and other JSON operations retain their
general binder. Ordinary caller argument packaging remains; no general calling
protocol, cache, pool, owner, allowance, parser or execution mode is introduced.
Duplicate input validation is consolidated. No public or memory contract changes.

Separate warmed native allocation diagnostic, **128 calls per row and API**:

| Call shape | Earlier bytes, sync / async callable | Candidate bytes, sync / async callable |
| --- | ---: | ---: |
| One positional argument | 19,456 / 19,456 | 8,192 / 8,192 |
| Keyword `s` | 19,456 / 19,456 | 19,456 / 19,456 |
| Explicit `cls=None` | 19,456 / 19,456 | 19,456 / 19,456 |
| Numeric hook | 38,912 / 47,104 | 38,912 / 47,104 |

This proves **88 fewer allocated bytes per eligible call**, its eight-slot array.
Arguments, callable/context/input construction and warmup are outside counts.
The fixed input is `7`; these are native callable invocations through its sync/
async interfaces, not public whole-script Run/RunAsync measurements. No result
implies whole-script allocation, live-memory, RSS or latency improvement. The local
Windows 11/i7-14700KF diagnostic uses Release, SDK
10.0.300-preview.0.26177.108 / runtime 10.0.13 and
`GC.GetAllocatedBytesForCurrentThread`, without forced GC. Raw
[before](native-baseline.json)/[after](native-candidate.json), process/TRX receipts
and [source](allocation-diagnostic.cs) are retained; the temporary probe is removed
from supported tests and final assemblies.

A preceding **ten-second all-thread CPU profile** of the unchanged JSON helper
job selected this boundary: 7,952 samples, zero lost events or missing stacks,
265 unresolved leaves (3.33%). JSON callable/loads/decoder/parse appear in
22.81%/20.70%/20.28%/19.77% inclusive stacks. They overlap and do not predict
savings. Ordinary tiering/JIT work is included. The prepared source matches all
546 inspected source files at 6c9a51df, while its assembly revision metadata names
e470ccc2 with the earlier one-file overlay. It is attribution of the preceding
source, not a before/after CPU comparison or the candidate binary. See
[assessment](profile.json), [CPU summary](profile-cpu.json),
[source validation](profile-source-validation.json), [runner](profile-runner.py),
[responses](profile-responses.jsonl.gz) and [terminal cleanup](profile-terminal.json).
The raw trace/archive is retained with its recorded hash in local agent notes.
The owned run took 14.453 s; all 444 responses / 886 invocations match goldens,
sources/inputs are unchanged, worker/tracer/fixtures are gone and lease released.
No second trace or profiler installation ran.

The complete comparison reuses **4,096 records / 110,592 UTF-8 input bytes**, the
unchanged helper script, sorted 25-byte report and stdout `3072\n`. Warm reused
compilation, fresh state, completed-host public `RunAsync`, prior-result disposal,
file staging/reset, capture/projection and full file checks are timed. Compilation,
startup, transport, response hashing, real suspended I/O and embedding latency
are outside. No fixture is retuned.

Dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS, Linux 6.17.0-1022-azure,
four AMD EPYC 9V45 vCPUs, approximately 16 GiB RAM, no swap. Both Release builds
use SDK 10.0.401 / runtime 10.0.12. CPython uses GCC 13.3, PGO/LTO, ordinary
GIL/GC, no JIT/debug/free-threaded mode and `-I -S -X utf8`. Workers remove the
supervisor tiering override and keep ordinary GC/tiering, limits/checks and an
explicit unbounded shared allowance. Fuel is unset; no finite-budget/RSS claim.

The working one-file candidate is on immutable
6c9a51df1796346c522b47ee41f0019a98a941b0; it is not a later HEAD measurement.
[Source identity](frozen-source.json), [patch](candidate.patch),
[preparation](preparation.json), build logs and canonical source in the
[raw archive](raw-evidence.tar.gz) identify it. Clean baseline binaries were frozen
before overlay; the source checkout then has the one candidate modification.
Assembly revision metadata names the base. Responses/identities are in
[micro.json](micro.json).

The sole comparison uses the existing 15-second collection / 30-second owned
group cap and shared lease, one-second warmups, two-second settling and seven
rotating 25 ms batches. Total **9.662 s**; **37 responses / 469 invocations** match
goldens/digests, twelve inputs and 55 frozen baseline files remain unchanged,
workers/fixtures are gone, the group is empty and the lease is released. See
[validation](validation.json), [ownership](ownership.json), [launch](launch.json)
and [terminal](terminal.json). No recapture, variant campaign or full lane ran.

Local Release: **588 focused checks** pass for binding/argument effects, aliases,
defaults, custom decoders, hooks and real suspension/cancellation, parsing/errors,
ownership/admission and complete-file goldens. All 70 white-box JSON test names
and their frozen JSON fuel/span/effect output records match the prior runtime exactly.
Six independent snippets match CPython 3.13.2 through each API. See
[local validation](local-validation.json). No timeouts, Stress scales, denial
demands or containment assertions are relaxed. Stress is retained, not rerun.
Explicit Release and cross-platform qualification belong to the runtime delivery.
