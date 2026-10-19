# Native field conversion before async machinery, 2026-10-19 UTC

Native field values now invoke the existing synchronous formatter through an
immediate ValueTask result. Only guest instances enter the descriptor/protocol
async body. That body moves verbatim apart from indentation and its explicit
value local. Seven immediate-await callers in executable/lowered fields and
urllib keep their evaluation, effects, funding, exception and cleanup paths.
No second formatter/dispatcher, owner, cache or callback mode is added.

Baseline:265ac1ce3d77eeab482d605276d9723230d99979;candidate:that revision plus
this [one-file patch](candidate.patch). One frozen native allocation pair uses
prepared code/context/input,two warmups,eight samples,eight calls/sample.
Compilation,startup scheduling,public projection and host I/O are excluded;
pure async operations complete on the measured thread. Native exceptions now
throw synchronously into the same immediate-await callers rather than first
passing through the inner async helper's exception machinery.

| Measured native invalid-specifier call | Before | After | Saved |
| --- | ---: | ---: | ---: |
| Sync execution | 5,304 B | 3,496 B | 1,808 B |
| Async execution | 8,680 B | 7,424 B | 1,256 B |

All22 other allocation arrays match exactly,including successful native calls,
guest instances,descriptors and guest faults. All work/funding/output/failure
records and472 fuel/admission records match (408 fuel,64 admission). The fault
saving includes changed CLR exception/task/state processing;it is not attributed
to the size of one task. Source proves less async machinery on native values,
but no successful-call latency or CPython speedup is established. No timing or
recapture was performed. A wider calling-convention/async-interpreter redesign
is outside this delivery.

All831 distinct focused Release checks pass:144 white-box,453 public formatting/
protocol,82 URL compatibility/host/projection,90 ownership/refusal and62 complete-
job/cancellation cases (58 complete CSV/text/JSON/file jobs). Existing tests cover
real descriptor/protocol I/O,cancellation,first refused admission,retained results,
G03 deep stack admission/recovery and all seven callers. Twelve independent
programs match CPython3.13.2 through both APIs (24 runs),including native/guest
faults,descriptors,generators and URL conversion.

There is no public behavior/ownership/fuel contract change. Existing nested
container and str.format I/O boundaries remain documented in SPEC. No permanent
architecture test is added;the temporary diagnostic is removed. Original Stress
scales/assertions/timeouts remain unchanged;no fresh Stress pass is claimed.
Managed stack admission and actual allocation preflights are retained.

Local Windows10.0.26200 X64,i7-14700KF/28 logical processors,68.4GB;
SDK10.0.300-preview.0.26177.108/runtime10.0.13. Allocation only,no quiet-machine,
elapsed,compile-to-result or complete-job performance claim. Package/platform
qualification is recorded in the private plan. Regex remains planning-only.
[Scope](measurement-scope.json), [source](source-screen.json), [diagnostic](diagnostic.cs),
[baseline](baseline.trx), [candidate](candidate.trx), [comparison](comparison.json),
[CPython probes](candidate-probes.json) and [validation](validation.json) preserve
all evidence. Stop at this helper and its direct callers.
