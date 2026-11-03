# Same-runtime Run versus RunAsync — B161

2026-11-03. Diagnostic of **873ae625**, no runtime prototype. All three images
load identical B160 core bits; `before`/`before-control` use `Run`, `candidate`
uses awaited `RunAsync`. Existing complete CSV/text/JSON/report sources, inputs,
outputs and file checks are unchanged. The benchmark host completes owned byte
file operations synchronously under both APIs, so this does not measure actual
I/O waiting. It tests the API/execution-strategy difference on useful scripts.

Quiet Azure F4as_v7, Ubuntu24.04.4, .NET11 RC3, normal GC/tiering, tmpfs files.
Reuse the private complete-job driver, changing only invocation of the candidate
API and its result retrieval. Three isolated images share CoreLib/hashing and
the same binary/auxiliary files.512 interleaved warmups per image, two seconds
settling per case, nine rotated32-job batches. No new benchmark framework, runtime
policy, opcode counter, watchdog or production dependency.

| Complete script | Run ms | RunAsync ms | Elapsed Δ / Run control | CPU Δ / Run control | Allocation Δ |
| --- | ---: | ---: | ---: | ---: | ---: |
| CSV summary |5.430|6.589|+21.36 /−0.38%|+22.67 /−0.54%|+6.575%|
| Text summary |3.663|4.497|+22.76 /−4.28%|+24.83 /−4.29%|+0.032%|
| JSON-lines |5.622|6.833|+21.55 /+1.29%|+22.69 /+1.56%|+0.030%|
| Report |5.941|6.603|+11.14 /+1.18%|+13.62 /+2.94%|+0.018%|

Positive means more cost under RunAsync. Candidate IQR0.46/0.70/0.20/1.55%; Run
control0.33/0.31/0.36/1.47%. Text originals drift4.28%, which limits precision
without removing the large API difference. These are diagnostic medians, not
independent process/machine repetitions or an external baseline.

The table's complete clock includes fixture reset, invocation, mediated guest
I/O, output checks and result disposal; compilation/startup excluded. A nested
Run-only clock excludes reset/check/disposal and measures API completion: its
differences are+21.96/+22.97/+21.88/+11.76%, with controls−0.10/−4.35/+1.33/+1.41%.
RunAsync reflection/task-result retrieval is included. Process CPU is total
driver/runtime CPU; all execution workers finish before cleanup and disposal.
No CPython ratio, source speedup or prediction of asynchronous-I/O latency.

Text/JSON/report allocation differs only about0.02–0.03%. Thus per-instruction
heap allocation is not established as the explanation. CSV allocates about361,831B
more per run, roughly88B/record; its exact temporary representation is not yet
attributed. Accounted execution/root peaks are identical across both APIs for
every workload. These are coarse accounting values, not physical RSS. Goldens,
zero balances, image isolation, shared hash witnesses and cleanup all pass.

## Consequence for the runtime plan

The source already shares executable function bodies/native-helper returns and
uses synchronous native arithmetic/subscripts under RunAsync. Startup workers
are reused and return to their pool at genuine suspension. Those completed fixes
are not reopened. The remaining source has separate sync/async instruction loops,
generic local-transfer dispatch under async, repeated native routing and uncached
general async member lookup. This justifies a coherent shared-dispatch design;
it does not assign an exact fraction of the gap to one method.

Selected next milestone: one synchronous instruction-prefix driver, with an async
outer loop waiting only for incomplete operations or pending disposal. Reuse
existing operation/abrupt handlers, preserve the synchronous native path and await
real protocols/hosts/cleanup. Remove displaced duplicated dispatch. No per-row
recursive continuation chain, new frame owner, arena, backend or public mode.
The [source/design gate](source-gate.md) records lifetime/error/cancellation/refusal
acceptance and the bounded both-API source-pair comparison. Overall synchronous
CPython performance remains a separate open objective; this API gap cannot explain
those published synchronous comparisons.

## Identity and completion

Core SHA256 `a890ccf44ea9ee7ccbae436cfc64b39bb6367191f1a9aaf42c8ef5388ac2da79`
in every image/case. This is the already-frozen B160 measurement binary; committed
source is873ae625 and its source hashes are recorded with B160. No new runtime
build or source change was used here. The owned service exits0 in68.584s; unit
inactive/dead, shared lease independently free. No worker remains running.

[Observations](observations.tar.gz), [assessment](assessment.json),
[identity](identity.json), [adapted existing driver](Program.cs),
[collection command](collect.sh), [terminal service](vm.log).
