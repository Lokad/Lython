# Configuration invocation attribution — 2026-11-08

R101/B460 at committedc74c5802/core3fb37cc3 closes the bounded setup investigation.
R100 source facts already find unboxed sum accumulation, executable imports and
shared immutable builtin functions/signatures. Exception values have lazy metadata;
naive sharing is not selected from source alone. Public CLR-ingress/BigInteger
projection contracts also prevent casually replacing guest integer representation.

One unchanged complete maintained config-read/edit/write script:16paths/181B,
same source/fixtures/full stdout/file goldens as R97. Six-second wall capture,
4.240s summed command-v2 clocks,2.93guestCPU-s/552samples, CPU-clock/FP199Hz,
6.078command-second warmup/two-second settle. Warmup2048invocation chunks (max64);
capture256invocation batches,52736complete invocations. Compilation/startup excluded.

| Visible boundary | Inclusive guest samples | Guest sample share |
| --- | ---: | ---: |
| Builtin factory |19|3.44%|
| Complete execution-context constructor |35|6.34%|
| JSON work |92|16.67%|
| Comparison file host |274|49.64%|

Inclusive groups overlap and cannot be added as independent costs.333kernel
leaves(60.33%) show filesystem/scheduling work; this is not all attributable to a
single capability check. All552stacks contain mapped core frames;31unresolved
leaves(5.62%) remain. Prefix83.5%inclusive includes nested host/JSON work, not
dispatch self-cost. Guest sampling excludes fixture reset/full-validation work on
host-worker threads, protocol and other process threads. This does not decompose
the guide's elapsed measurements or compare normal-settings performance.
PerfMap/JIT diagnostics affect execution; Ubuntu24.04.4/F4as_v7/.NET11RC3,
optional0.4/worker7cf37ca5 unchanged.

The evidence does not support a dominant builtin-setup bottleneck or justify new
lazy factories/shared mutable roots. Keep host authorization/component checks.
JSON's16.67%inclusive presence warrants a bounded source check, not a speedup
prediction: the decoder already keeps completed values in its ordinary loop.
The encoder still uses actual async Advance/AdvanceValue/PushDictFrame/TryTakePair
methods for native values. The next hypothesis is sharing their completed path
without per-step state machines, retaining existing frames and true suspension.
Any prototype must preserve semantic chunks/options/hooks, admitted owners,
cancellation/first refusal and cleanup and pass a bounded complete-script source pair.

All source/core/auxiliary/Ready/pre-post full goldens/quiet/Closed/exit/join/
fixture/map-copy/archive/local verification gates pass. B460 terminal/cached,
no workers or RAM traces remain. Exact existing B459 core and archive reused/
verified; raw/stacks/maps/JIT listings/unknowns stay persistent. Footprint+25%
and actual payload preflight fit8MiB above240MiB OSfloor. No new core, producer,
runtime framework or CPython replay. Full source qualification is independently green at
[cached CI42479543467](../2026-11-08-default-rsplit-preparation/delivery-ci.json).

[Attribution](attribution.json), [identity](identity.json), [core provenance](core-source.json),
[status](status.json), [raw observations/source/maps](observations.tar.xz).
