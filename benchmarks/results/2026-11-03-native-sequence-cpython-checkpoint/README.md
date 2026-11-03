# B172: current native-sequence milestone versus CPython

2026-11-03, retained runtime source **b7a0f40a**, core
**6d4b0c32961c01a1fe1f975585ceb01b3791ca24f1cd24b5750f60dac01161ed**,
frozen before its source commit. CPython **3.13.16**, .NET 11 RC3, quiet dedicated
Azure Standard_F4as_v7/Ubuntu 24.04.4. Ordinary GC/tiering/GIL, no JIT/runtime overrides.
This is a same-source external comparison, not a source-pair improvement claim.

| Complete RAM-file script | Fresh Lython / CPython | Warm Lython / CPython | Status |
| --- | ---: | ---: | --- |
| CSV helpers, 4,096 records | 5.514 /3.510 ms =1.57× | 5.249 /3.316 ms =1.58× | Diagnostic; controls within 0.7% |
| JSON lines/helpers, 4,096 records | 5.850 /4.223 ms =1.39× | 5.648 /4.084 ms =1.38× | Diagnostic; warm control −1.9% |
| Text/helpers, 3,072 lines | 3.524 /1.301 ms =2.71× | 3.311 /1.156 ms =2.87× | Diagnostic; fresh control +2.7% |
| Report, 3,072 JSON records | 5.684 /2.466 ms =2.31× | 5.531 /2.366 ms =2.34× | Variable fresh; Lython/CPython IQR 3.8%/2.4% |

Ratio is Lython elapsed divided by CPython elapsed; above one means slower. The
remaining text/report gap is substantial. Fresh and warm are independently warmed
process medians and must not be subtracted to infer compilation. Compile-only CSV
**85.57/54.98 µs (1.56×)**, report **59.94/37.50 µs (1.60×)**; identical Lython
controls +0.4%/−0.2%. Startup is unmeasured. Earlier b087 points remain immutable;
their differences are not controlled source-pair gains.

## Boundary and controls

Existing B163 worker/protocol/collector, ten jobs: four warm, four fresh compile-
to-result and two compile-only. Each uses isolated current Lython/current unchanged
control/CPython workers. Three-second warmup, nine rotated approximately 200 ms
batches; source/fixture/full stdout/file digests verified throughout. All 30 workers
close, join and remove owned fixtures. Quiet gates pass before/after; service exits
zero in **166.214 s** (collection interval 165.570 s), shared lease released.

Actual host file APIs use shared tmpfs; both clocks include invocation, mediated
guest I/O, complete output/file checks and prior-result cleanup within the batch.
Fresh Lython compiles/disposes each per-invocation program inside that clock.
Fixture reset/staging is excluded symmetrically by protocol 2. Startup, transport,
final retained result delivery/disposal, cached program teardown and worker exit
are outside each batch timer but finish before join. Compile-only has no guest
execution/I/O; never derive it by phase subtraction. Host file overrides are the
existing efficient owned-file implementation. Actual suspended protocols/I/O and
cancellation are separately tested; these executions use Run.

Lython invocation caches remain isolated; CPython process module/library caches
persist with fresh execution globals, matching the public integration boundary.
Both use their supported annotation and Python semantics. No CPython hash-seed
alignment, disabled containment, shortcut golden, new benchmark framework or
ten-minute lane. Controls, warmups, outliers, precise phase clocks, process CPU
ticks and binary/toolchain identities remain in raw observations. Instrumented
allocation profiles are different evidence and do not supply these timings.

The authoritative guide is updated with these four current rows while retaining
older config/filesystem cases under their own source/storage labels. No changed
runtime is inferred from observer source identities; exact core hashes are checked
at worker readiness. Core delivery qualification belongs to the separately tracked
B171 source checkpoint, not this timing collection. Publication remains postponed.

[Assessment](assessment.json), [complete observations](observations.tgz),
[source identity](identity.json), [exact scripts/fixtures](representative.json),
[prior external checkpoint](../2026-11-03-shared-driver-cpython-checkpoint/README.md),
[B171 source-pair controls and report tradeoff](../2026-11-03-native-sequence-operands/README.md).
