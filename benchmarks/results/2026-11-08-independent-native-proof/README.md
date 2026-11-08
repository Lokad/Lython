# Independent native proof, intermediate

This preserved intermediate is superseded by the
[qualified combinedb1e17b64 delivery](../2026-11-08-primitive-helper-delivery/README.md).
Its own frozen intermediate was not separately fully qualified; original capture
status and all adverse conditions remain in its archive.

2026-11-08. Combined R113/R114 core a383d084 over9b61264b, compared with fully
qualified05790317/core13107e6b. Incremental local diagnostics compare with R113
core d8d2a6fb; do not subtract percentages across those different runs.

Existing proof nodes now own immutable opcode metadata and member/return spans;
ordinary operand forwarding runs independently, including failed native guards.
Runtime source/return/member instruction offsets and the native/forwarding mutual
exclusion are removed. No new evaluator/backend/owner/cache/arena/public option.
Native nodes grow32→72B; ordinary instructions remain32B. Incremental compilation
allocation+1.07–1.09%accounting/+0.59%text, report unchanged; warm allocation neutral.
1524public/104internal focused peractual11/10 pass before conservative casing
block-bound refinement, exact final63public/51internal pertarget pass afterward.
Full qualification pending; this is an intermediate prototype.

B474: same quiet F4as_v7/frozen7cf worker/optional0.4/normal GC/tiering,
two isolated roles/core,5swarmup,7rotated150msrounds. Eight jobs223.498s; complete
goldens, budget baselines, Ready/image/quiet/Closed/join/fixture/archive/local
verification pass. No users/ownedRAM remain. Warm includes invocation, mediated
RAM I/O, full validation and prior-result cleanup; compile/startup/staging/final
cache teardown excluded. New accounting variations have independent CPython
semantic goldens; this is not a refreshed external performance comparison.

| Script / API | Elapsed change, all pairings | CPU change |
| --- | ---: | ---: |
| Integer CSV / sync |−6.33% to−2.94%|−6.25% to+2.99%|
| Integer CSV / async |−9.96% to−5.10%|−10.71% to−1.23%|
| Integer JSON / sync |−6.41% to−0.85%|−7.14% to0%|
| Integer JSON / async |−7.44% to−5.78%|−7.69% to−3.85%|
| Float CSV / sync |−0.09% to+1.18%|−6.25% to+4.17%|
| Float CSV / async |+4.14% to+6.29%|0% to+9.09%|
| Unchanged text / sync |−5.00% to−0.29%|−2.44% to−1.22%|
| Unchanged report / sync |−2.50% to+1.46%|0% to+2.38%|

Negative is faster. Synchronous integer controls vary; async float is adverse
and requires a bounded correction. Generalize the same proof to ordinary primitive
float/bool arithmetic using canonical operators and one shared compiler/runtime
shape fact. Keep custom protocols ordinary and measure a genuine guard-miss control.
Repeated proof attempts are source-visible, not an isolated explanation of all CPU.
Do not revive slower call-control/arena carriers or silently waive the regression.

Original metadata error: setup/observer says owner210, preplanned contract and
applied acknowledged launch command configure270s; collector reserves final work
after240s, scheduled observation+330s. The terminal transient unit reports infinity,
so its earlier live limit is not independently verified. All original files are
preserved; no retrospective timeout-enforcement claim, requery or duplicate run.
Bounded exact-byte hardlinks recovered11.09MB with fresh idle-lease identity/alias
proof, no archive/path/content deletion or relaxed free-space guard.

[Status](status.json), [all roles](assessment.json), [identity](identity.json),
[incremental allocation](incremental-allocation.json), [compilation](incremental-compilation.json),
[managed metadata size](metadata-size.json), [receipt](archive-receipt.json),
[all sources/binaries/corrections/raw archive](observations.tar.xz).
