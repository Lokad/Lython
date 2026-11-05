# Qualified pipeline checkpoint against CPython

2026-11-05. Runtime source **cf3dc9cd**, frozen precommit **43fdf658** with assembly
metadata ca05da4b; exact snapshots/hash identities authoritative. Final delivery
41944972624 passed all eight Windows/Ubuntu actual.NET11/10/original Stress/
installed Release consumer gates. [Terminal cached receipt](../2026-11-05-cased-whitespace-pipeline/delivery-ci-status.json).
Publication postponed. Core remains independent of the optional regex producer.

Reuse unchanged protocol2 and six maintained complete scripts on the quiet Azure
Standard_F4as_v7, Ubuntu24.04.4, four EPYC9V45 vCPUs/16GiB/no swap. .NET11RC3,
ordinary GC/tiering, CPython3.13.16 GCC13.3 PGO/LTO with ordinary GIL/GC/no JIT.
Four isolated process roles: current, identical-source control, CPython and
previous external f6e51d1c/df206f47. No new framework/benchmark cases or runtime
overrides. Original14jobs complete384.95s below480s owned bound,5s wall warmup/role,
7rotated150ms batches. Exact identities/normal settings/full independent stdout/
file goldens/before-after quiet/closed workers/fixture-removal gates pass.

## Current complete-script observations

Ratios=Lython elapsed/CPython elapsed; above one means slower. First six rows of
the [authoritative guide](../../../docs/BENCHMARKS.md) use fresh compilation plus
one complete synchronous execution inside an already-running host.

| Complete script | Fresh Lython / CPython | Fresh ratio | Warm Lython / CPython | Warm ratio |
| --- | --- | ---: | --- | ---: |
| CSV summary/helpers |5.030 /3.444ms|1.46x|4.960 /3.363ms|1.47x|
| JSON-lines/helpers |5.107 /4.122ms|1.24x|4.828 /4.090ms|1.18x|
| Text summary/helpers |2.95–3.15 /1.26–1.29ms|2.29–2.50x|2.782 /1.201ms|2.32x|
| JSON-to-Markdown report |4.368 /2.504ms|1.74x|4.107 /2.347ms|1.75x|
| Config read/edit/write |0.265 /0.255ms|1.04x|0.191 /0.185ms|1.03x|
| CSV long fields |0.401 /2.427ms|0.17x|0.249 /2.301ms|0.11x|

Fresh text ranges cover current/control role medians in the original collection
and its one confirmation, not confidence intervals. Ratios are paired within
collections. Original current2.952ms/control3.146ms vsCPython1.257ms has6.54% spread.
Same binaries/source/fixtures,10s warmup/reversed worker order/250ms batches give
current3.108ms/control2.953ms vsCPython1.287ms,4.98% spread. That confirmation55.34s
passes all original identity/golden/quiet/owned-cleanup gates; its conditions are
different and do not identify the cause. Both complete collections remain visible;
stop with this uncertainty, without further automatic retiming or a design veto.

Warm text control0.77%/currentIQR0.54% is separate. Report remains variable:
fresh control2.25%/IQR3.03%, warm2.86%/2.27%. JSON warm control2.22% limits small
claims. All medians/IQRs/adverse roles are retained in the linked JSON summaries.
No result is silently upgraded or removed. Earlier907 search remains inconclusive
and earlier ordinary-filesystem cases retain their own dates/sources in the guide.

**Compile only:** CSV71.91us vs54.60us (**1.32x**), report51.12us vs38.16us
(**1.34x**). CurrentIQR0.54%/2.59%, controls2.13%/0.96%. These are independent
public compilation/validation/disposal measurements, excluding guest execution/IO;
do not derive compile cost by subtracting warm/fresh medians. Startup unmeasured.

Compared directly with previous externalf6 in these conditions, warm text favors
current source about6% with usable same-source controls. Fresh-text magnitude is
uncertain. JSON/report favor current with control/dispersion limits; typical CSV
is near unchanged, configfresh favors current/warm near unchanged, compilation
favors current about9–10%. These are coupled diagnostic effects, not attribution
to individual changes or additive gains. The earlier JSON-control~1% and fresh-
config adverse source-pair observations remain; current combined data does not
erase them or promise future CPU gains. Substantial text/report gaps remain.

## Execution boundary and evidence

Both clocks include public invocation, actual mediated RAM-file IO, complete output/
file checks and prior-result disposal. Fresh adds compilation and its disposal.
Fresh globals are used; ordinary CPython process module caches persist while
Lython execution caches remain isolated. Efficient ranged-read/append host overrides
are used. Fixture reset/staging, startup, response hashing and final cached-owner/
process/fixture teardown are excluded. Every worker is joined after cleanup.
Current scripts use Run; genuinely suspended IO/cancellation is qualified separately.
No heap/RSS/embedding-latency, guest-dispatch-only CPU or asynchronous-speed claim.

- [Original premeasurement contract](initial/pre-measurement-plan.md),
  [summary](initial/summary.json), [identity](initial/identity.json),
  [raw observation archive](initial/observations.tgz), [archive receipt](initial/archive-receipt.json).
- [Bounded confirmation contract](text-confirmation/pre-measurement-plan.md),
  [summary](text-confirmation/summary.json), [raw archive](text-confirmation/observations.tgz),
  [receipt](text-confirmation/archive-receipt.json).
- [Combined status/range calculation](assessment.json). Original raw/source
  snapshots remain immutable; archives contain exact existing collector/catalog/
  worker/gates and every sample. Earlier f6 observations remain in their own folder.

Original raw SHA256:
`1e8e38607fe285bf9f1b5f927a72c6aad547a6dbbd1f9858b501c7bc2cd56801`.
Confirmation raw SHA256:
`aff164249ee070eb4be374058bd8995bb8dc662e0d98076b41e7a1773f421607`.
