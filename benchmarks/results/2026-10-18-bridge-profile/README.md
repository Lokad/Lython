# Bridge file-job CPU attribution: closed without a performance change

One ten-second whole-process CPU profile does **not** justify an invocation-setup
optimization or explain the CPython timing gap. Ordinary tiered JIT, file-fixture
reset and protocol/scheduling are material. This investigation is closed without
another trace, timing capture or runtime performance claim.

| Frame | Samples | Fraction of all CPU samples | Interpretation |
| --- | ---: | ---: | --- |
| Tiered JIT background work | 1,025 | 31.10% | Ordinary JIT remains active |
| Comparison protocol read | 899 | 27.28% | Includes work outside timed batches |
| File-fixture reset | 649 | 19.69% | Benchmark host staging/reset |
| Lython RunAsync | 528 | 16.02% | Includes descendants; not setup alone |
| Native spin-wait leaf | 273 | 8.28% | Leaf CPU attribution only |

Inclusive rows overlap and **must not be added**. These are CPU samples, not
wall-clock shares or predicted savings. The diagnostic uses two-invocation
batches, while [CD's timing comparison](../2026-10-18-json-default-dumps/README.md)
calibrates batches. Protocol work outside the timed invocation is included here.
One trace cannot establish steady-state proportions or attribute the CPython gap
to a particular Lython mechanism. No builtin-construction change was selected.

Measured **2026-10-18 UTC** on the same dedicated Azure Standard_F4as_v7 VM:
Ubuntu 24.04.4, four AMD EPYC 9V45 vCPUs, 16 GiB RAM without swap, .NET SDK
10.0.401 / runtime 10.0.12. The existing installed dotnet-trace 10.0.750501 tool
collects `cpu-sampling,dotnet-common`; TraceEvent 3.2.8 analyzes the trace.
These are development tools, not new Lython runtime dependencies or capabilities.

The unchanged bridge JSON job reads 42 bytes, edits an array and writes the
independent 46-byte file golden. The boundary is warm public RunAsync with
completed host operations, reused compiled code and ordinary GC/tiering. Fuel is
unset; execution/result ownership remains active under the existing unbounded
shared allowance. This does not measure compilation, startup, genuinely suspended
I/O, embedding latency, before/after CPU, RSS or a finite-budget workload.

All **546 canonical runtime/comparison source files** match
`432b11c0dcbe1315c0603c2b6facfa2ae0f3fbc3`. The reused CD worker retains
`22648c5a364877801ff9c11ba6bdc6fb75d631c7` build metadata, with four frozen runtime
files applied; the two identities are intentionally distinguished. Recorded source
and input/binary hashes are unchanged before/after capture. See [diagnostic](diagnostic.json)
and [expected source hashes](expected-source.json).

The trace has **3,296 samples**, zero lost events or missing stacks, and 81
unresolved leaves (2.46%), reported without assigning them to runtime code.
All 3,559 responses / 7,116 invocations match stdout and complete-file goldens.
Owned duration is 13.716 s under the existing 30-second process-group cap and
shared VM lease. Worker, tracer and fixture directory are gone; the lease is
released. See [assessment](profile.json), [CPU summary](cpu.json),
[responses](responses.jsonl.gz), [terminal proof](terminal.json),
[runner](runner.py), [tracer log](tracer.log) and [unaltered archive](raw-evidence.tar.gz).

Two preflight corrections preceded the sole actual worker/tracer capture. First,
the inline source manifest exceeded Windows' command-line limit before SSH
started; files were transferred through SCP. Then the source guard rejected
expected hashes generated from Windows git-archive CRLF content. Canonical LF
content was checked before the actual capture. Neither preflight started a worker
or tracer. Their [correction record](preflight-correction.json) and
[failed preflight archive](preflight-evidence.tar.gz) are preserved.

Separately, a previously identified cleanup removes the unused `object owner`
parameter and two arguments from prepared-definition storage admission, naming
the helper `AdmitPreparedDefinitionStorage`. Size arithmetic and reserve/commit
calls are unchanged. There is no Python/public/memory contract change, new owner
or measured performance claim. No new tests or benchmark were added for this
internal signature cleanup. **133 focused existing checks** pass across prepared
definitions, defaults/decorators, retained lifetimes, frame/fuel, admission,
managed stack bounds and async cleanup. See [local validation](local-validation.json)
and [artifact validation](validation.json). Original Stress scales and containment
assertions remain; full Stress was not rerun.
