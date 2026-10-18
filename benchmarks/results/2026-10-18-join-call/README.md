# Direct ordinary join calls, 2026-10-18 UTC

One-positional-argument immediate calls on exact string receivers reuse the
existing receiver/operand/native-call convention. They avoid eligible bound
callable closures and argument packaging. Exposed, expanded and custom calls
retain ordinary binding. Both paths share the corrected leased join operations;
full consumption, validation, UTF-8 builder, admission and ownership are unchanged.
No new binder, cache, owner, iteration framework or memory infrastructure.

Baseline: `c3d5a2d6e5da2f24ceff2898215ae2ff92343462`. Candidate: that revision
plus the [six-file runtime patch](candidate.patch), including permanent regression
coverage. The [scope](scope.json) and fixed gate precede production edits.

| Native allocation per call, 16 Unicode parts | Baseline | Candidate | Saved |
| --- | ---: | ---: | ---: |
| Immediate exact list, Run | 2,456 B | 2,104 B | 352 B |
| Immediate generator, Run | 3,672 B | 3,320 B | 352 B |
| Immediate exact list, RunAsync | 2,496 B | 2,176 B | 320 B |
| Immediate generator, RunAsync | 3,720 B | 3,400 B | 320 B |
| Exposed / expanded / custom controls | See comparison | Identical sample arrays | 0 B |

One qualified native pair uses two warmups, eight samples and64 calls/sample,
ten allocation routes across both APIs. All448 exact result/UTF-8/error/message/
span/callback/fuel records match. [Comparison](comparison.json),
[diagnostic](diagnostic.cs), [baseline raw results](baseline-native.trx),
[candidate raw results](candidate-native.trx) and process receipts retain evidence.
Before production edits, the initial fixtures had two native and two public
failures: the handcrafted custom type lacked object's attribute behavior, and an
invalid literal argument was rejected statically before I/O. Corrected custom
inheritance and valid suspended-argument success/cancellation controls pass on
unchanged production. [Fixture receipts](fixture-corrections.json) preserve those
trials; the gate was unchanged. The qualified capture pair was not repeated.

Local Windows x64 build26200, Intel Core i7-14700KF (28 logical processors),
.NET SDK10.0.300-preview.0.26177.108/runtime10.0.13. [Environment](environment.json)
reuses unchanged machine configuration with its original capture timestamp.
Allocation covers warm completed expressions on one context, excluding frontend
compilation, setup, public projection, scheduling and startup. No elapsed-time,
complete-job, CPython speedup or total pending-I/O allocation claim. The previous
join correctness change's materialization cost remains documented separately;
this patch retains its admission and error-order contract.

All537 distinct focused Release cases pass, including58 complete CSV/text/JSON/
file jobs. Eight new source-linked cases prove direct receivers avoid bound-method
caches and whole-invocation result refusal preserves inputs with scratch live.
Six new public cases cover receiver rebinding and custom methods; two more cover
actual suspended argument success/cancellation. Existing iterator suspension,
late effects/errors, retained results/caches, terminal refusal and shared-owner
cleanup remain. All28 public join cases also pass on unchanged baseline.
[Eleven CPython goldens](goldens.json) support22 pure program/API combinations:
three freshly verified programs plus eight immutable prior goldens. Details and
receipt/TRX hashes are in [validation](validation.json). Temporary diagnostic
removed; original Stress scales/assertions/timeouts retained. Release/platform
qualification is recorded in PLAN; regex remains planning only.
