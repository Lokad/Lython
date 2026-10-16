# Fixed builtin definitions — 2026-10-16

Three fixed plain functions (`len`, `repr`, `ascii`) reuse their existing callable
definitions. Per root execution this removes three wrappers, two temporary
parameter-name arrays and three signature-catalog lock/lookups. Signatures already
interned; this does not remove construction of all signatures or builtin metadata.
Private dictionaries and mutable object/type/exception graphs remain per run.
No new callable, binder, cache, ownership abstraction or dependency was introduced.

The [source gate](source-gate.json) records the bounded isolation assessment:
static delegates receive the current context; type-cache writes exclude these
names; `__self__` resolves the caller's module. Fixed trusted metadata uses existing
host/control headroom. Cross-execution callable identity can become stable.
Suspension inside len/rendering protocols remains a separate existing limitation.

| Engine | Revision | Median | IQR / median |
| --- | --- | ---: | ---: |
| Lython before | 66420bd6910be2e6edc39485ab3c083c22f61b40 | 23.318 µs | 3.7% |
| Lython after | a2653a619229076e93fa891aa27770b96c4f1c69 | 23.445 µs | 2.9% |
| CPython | 3.13.16 | 1.506 µs | 1.0% |

**No improvement established.** The candidate's 0.5% higher median is within the
observed spread. This existing `control.empty.control` case executes `pass` in a
warm host and exposes invocation setup/cleanup. It is an empty control, not a
useful script or a decomposition of the published workload gap. Compilation and
process startup are excluded. No qualified multiplier, host-latency qualification
or confidence interval follows. The source removal stands independently of timing.

One unchanged existing micro warm lane finished in 7.186 s including owned command
cleanup. No second capture, retuning, other lane, new producer or benchmark
framework. Normal Lython protections, GC and tiering remain enabled; fuel unset.
CPython uses normal GIL/GC and `-I -S`, without equivalent in-process containment.
Machine: dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS, four AMD EPYC 9V45
vCPUs / 16 GiB RAM; .NET SDK 10.0.401/runtime 10.0.12.

[Raw samples](warm.json), [frozen inputs](comparison-declaration.json),
[catalog](catalog-micro.json), independent empty stdout checks
([before](baseline-correctness.json), [after](candidate-correctness.json)) and
[cleanup/source proof](cleanup-source-proof.json) are retained. Both sets of all
524 production sources and shared comparison/Python tooling match frozen inputs;
seven workers have exited, services are inactive and the exclusive lease is free.

Two preparation mistakes are preserved: the initial catalog ID omitted `.control`,
stopping before verification/timing ([declaration](initial-declaration.json),
[error](initial-setup-error.txt)); initial proof manifests used canonical Git LF
blobs while the immutable archives contained configured CRLF C# files
([declaration](pre-proof-declaration.json), [error](initial-proof-error.txt)).
Physical manifests were derived from those original, unchanged archives and their
LF-normalized manifests match the predeclared Git-blob manifests. No measured
source/binary changed; the completed baseline build was reused and timing ran once.

Five new public controls pass before and after: sequential sync/async module/type
metadata and guest state, retained representations across another execution,
overlapping host suspension/cancellation under a shared budget and name shadowing.
Initial test assumptions about mutable `object`, inherited `__new__` and cancelled
file creation were corrected before the runtime edit; failed receipts remain in
private development evidence. All 2,569 ordinary white-box, 688 focused public and
25 governed/representative controls pass (overlapping groups, each under 30 s).
No new Stress or full local platform claim.
