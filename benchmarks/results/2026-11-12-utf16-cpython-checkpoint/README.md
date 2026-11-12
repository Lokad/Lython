# Qualified 41bd869b CPython checkpoint

2026-11-12, exact41bd869beee1bb53880da89124d4a1e98b2105df, frozen coref0b736b2,
fixed workerfec79 and optional adapter0.4; stock CPython3.13.16 on quiet Azure
F4as_v7, Ubuntu24.04.4, four EPYCvCPUs/16GiB/no swap, actual.NET11RC3 with normal
GC/tiering. [Full source/platform qualification](../2026-11-12-bulk-utf16/delivery/README.md)
matches all568 frozen production sources with explicit newline normalization.
No DLL is relabelled as a CI-built image; no newer regex producer is qualified.

Seven jobs complete in328.130s under420sowner/30sstop/30smargin. Two processes per
engine, seven rotated250ms rounds,12swarmup for captured text/UTF16,5sotherwise.
Independent source/fixture/output/library identities, full stdout/files, normal
runtime controls and quiet gates pass. Every worker joins with zero exit/stderr
(the Lython worker checks final shared balance zero), RAM fixtures are unmounted,
persistent raw evidence archived and localSHA verified. No users/sole RAM evidence
remain. The terminal state is cached, not polled again. Final free347,463,680B;
no old evidence/image was deleted or storage floor reduced.

| Complete script / phase | Lython | CPython | Lython / CPython | L / P control spread; maxIQR |
| --- | --- | --- | --- | --- |
| Captured text / warm | 1.475ms | 0.879–0.890ms | 1.66–1.68× | 0.01/1.28%;0.41% |
| Captured text / compile-to-result | 1.521–1.605ms | 1.036–1.040ms | 1.46–1.55× | **5.19**/0.37%;1.23% |
| UTF16 report / warm | 3.605–3.645ms | 2.448–2.478ms | 1.45–1.49× | 1.11/1.19%;2.73% |
| UTF16 report / compile-to-result | 3.630–3.643ms | 2.592–2.641ms | 1.37–1.41× | 0.37/1.86%;3.16% |
| UTF8 report control / warm | 3.051–3.090ms | 2.336–2.350ms | 1.30–1.32× | 1.29/0.61%;3.04% |
| Config / compilation | 37.11–37.30µs | 34.57–34.72µs | 1.07–1.08× | 0.53/0.43%;1.35% |
| Config / compile-to-result | 138.7–139.3µs | 124.8–125.1µs | 1.11–1.12× | 0.47/0.24%;2.20% |

Above1 means slower. Ranges span all four process-median pairings, not confidence
intervals. Captured-text compile-to-result has stable within-process distributions
but materially different Lython process medians; its magnitude remains uncertain.
Compilation/warm/fresh medians are separate observations and need not add up.
Changing CPython clocks across collections does not identify a Lython regression
or gain; retain the [prior guide](prior-BENCHMARKS.md), raw results and control data.
This bounded comparison informs priorities without another unchanged timing run.

Fixtures: captured text3,072lines/35,840B; reports3,072records/189,441B; config181B
JSON/16declared paths. Identical source/fixtures on both engines. Warm includes
synchronous invocation, mediated RAM I/O, full output validation and prior-result
cleanup; excludes compilation/startup/fixture reset/protocol/final cache cleanup.
Compile-to-result adds source compilation and per-invocation compiled-owner
cleanup inside a running host. Compile-only times construction/prior-owner
cleanup, with final compiled owner retained through the response. External CPU
also includes reset/protocol/worker work. Lython's efficient host still checks
capabilities/real path components; CPython uses builtin open and retains imports.
No startup, pending-I/O, RSS/heap-peak or embedding-tail claim follows.

The first launch failed at the idle-process check before staging/mount/workers:
the Python command matched its own argv. Excluding only that actual launcherPID
corrected the check; its original log/conditions remain in preparation.tar.xz.
No worker measurement was repeated for this correction. Observations archiveSHA:
ddd257c3347cd9d59794cf98e67b6951dc11f5e24ef8c058657f61180bb4bb4e.
Publication remains held; the substantial whole-script objective remains open.
