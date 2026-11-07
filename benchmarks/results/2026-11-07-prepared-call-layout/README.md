# Canonical prepared function-call layout — R31

2026-11-07. Parameter classification, positional/keyword arrays and name-to-slot
maps now belong to the prepared definition. Executable, lowered, lambda and
generator function objects share them. The immutable half of FunctionBindingPlan
construction disappears from repeated guest function creation. Mutable defaults,
keyword-name PyStrings, attributes, closures and identities remain private. The
same binder, direct operands, native helpers and deferred contexts remain in use.
Native generated layouts keep their existing capacity preflight and rollback
through successful default construction. Compilation uses its existing coarse
reservation; no cache, lease, tracing, hidden allowance or compatibility overload.

## Exact source and validation

Consumer49d40b00 plus the preserved patch/new files. Candidate net11 33384376,
net10 9a043078; reference49d40b00/R30 net11 75e60e66. checked-source-and-images.tgz
contains all checked production source, including the new metadata file, and both
exact images. Source inventory, loaded hashes, original command/build failures,
private driver/catalog and all observations are preserved. The compiler-prefix
draft is future work, excluded from this measured source.

Release solution builds with zero warnings/errors. Per actual .NET 11/.NET 10,
1,080 internal/4,616 public focused cases pass. Coverage includes defaults,
keywords/positional-only/variadics, methods, dataclasses, class cells, lambdas,
generators, annotations, real suspension, cancellation and shared ownership/refusal.
The new repeated-definition regression mutates one function's defaults, checks
independent siblings and generator/lambda defaults, then runs the same compiled
program again. Both APIs return to the compilation budget baseline after each
result cleanup and zero after program disposal. Four independent actual-target/API
probes match CPython3.13.2. The annotation effect-order probe also matches Python.

## Allocation and bounded comparison

Existing complete-job allocation driver: four cases, separate compile/warm/fresh,
32 warmups, five rotated16-job rounds, before/current/identical-before isolated
images, ordinary GC/tiering. Every golden, balance and fixture/isolation gate passes.
Class-factory warm allocation removes about38,038 B (10.65%); fresh−7.35%, while
compilation adds1,728 B (+1.26%). Text/CSV warm changes are below0.03%; config flat.
Class admitted execution/root peaks fall24,832 B; complete retained funding and
final zero balances remain checked. This is ownership migration, not a smaller
live-object estimate. Desktop class clocks are adverse and retained; other desktop
controls vary substantially. They do not establish CPU effects.

B384, quiet F4as_v7/Ubuntu24.04.4/.NET11RC3, unchanged b94fa98c worker/host/normal
0.4 adapter. Six jobs, two before/two current processes, five-second warmups,
seven rotated150ms rounds. 165.61s; all identity, output/file golden, funding,
quiet, Closed/join/fixture and persistent-under-lease archive gates pass. Observed
after230s owner+30s stop+40s margin; no users remain.

| Complete job | Before roles | Current roles | All elapsed pairings |
| --- | ---: | ---: | ---: |
| Class factory, compile |41.381–41.502 µs|41.612–41.960 µs|+0.26–1.40%|
| Class factory, warm |170.482–173.524 µs|161.361–163.098 µs|−4.33–7.01%|
| Class factory, compile-to-result |231.918–233.344 µs|223.123–223.750 µs|−3.52–4.38%|
| Config, compile-to-result |147.290–148.210 µs|147.005–148.166 µs|−0.81 to+0.59%|
| Text helpers, warm |2.020–2.041 ms|2.016–2.104 ms|−1.26 to+4.18%, variable|
| CSV functions, warm |3.927–3.943 ms|3.911–4.108 ms|−0.81 to+4.60%, variable|

The synthetic factory builds64classes per invocation; these results are not a
representative CPython refresh. Compilation is separate; warm/fresh include
mediated RAM I/O, full validation and prior-result cleanup, fresh adds compile/
program disposal. Startup/staging/reset/final teardown excluded. Coarse external
process CPU is preserved without a causal CPU claim.

Retain the simplification and affected class-workload benefit. Accept the small
compile cost. Text/CSV are inconclusive: identical current roles differ4.41/5.02%,
so adverse pairings remain possible debt, not parity or demonstrated regression.
Do not claim their allocation reductions produce CPU gains. No favorable retiming
or source-pair compounding. Reassess this debt at the next coherent execution
change; do not silently close it or automatically reject the metadata design.

B383 fails before workers because a generated collector retains a five-job assert
for six jobs. No timings were collected; failure/source archive retained. B384
validates the actual collector/archive count both locally and remotely before
launch, reuses the same runtime/catalog and otherwise unchanged measurement.
Only exact redundant expanded B340 maps were retired after checking independent
pushed archive, current hashes/users/links; all original raw evidence remains.

Full platform, original Stress and installed consumers are batched with the
compiler-prefix consolidation at this coherent layout delivery boundary. Prior
R29 full qualification does not qualify this source. The broader performance
objective stays active; no publication or broad CPU improvement is claimed.
