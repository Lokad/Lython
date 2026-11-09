# Direct native callback arguments

R149,2026-11-09. Fresh committed46e3746e/coreDEDD85 versus the source freeze in
[identity](identity.json), candidate core6417B97A; common frozen worker/optional
adapter. Same whole script/input/golden per pair. These explicit map-based script
shapes are not timing comparisons against earlier differently shaped scripts.

Existing function/builtin binders consume short managed spans into owned binding
storage before default lookup or a body can suspend. Remove three thread-local
argument pools, six rent/clear/return helpers, forwarding async state machines and
duplicate function binding entries. Ordinary map calls use the same transport,
removing a per-record array/reservation. Wider map calls retain admitted arrays;
other callable implementations receive owned arrays because they may retain them.
No new pool, callable interface, public option or dispatch framework.

| Complete workload | Warm allocation, sync / async | Quiet elapsed, sync | Interpretation |
| --- | --- | --- | --- |
| CSV validation/aggregation | −6.8% /−6.8% | −2.8–3.2% | Modest observed improvement; coarse CPU varies −9.4–+6.7%. |
| Text word aggregation | −14.1% /−14.1% | −3.0–6.6% | Supporting CPU −2.5–6.3%; baseline process spread2.9%. |
| JSON report | −4.7% /−4.7% | −1.5–+2.7% | Mixed elapsed, no CPU improvement claim. |
| Unchanged report control | About unchanged | −0.6–3.9% | Controls also move; avoid attributing all elapsed differences. |
| Unchanged file-search control | About unchanged | −2.6–+2.9% | Inconclusive. |

Compilation allocation unchanged across all five. Original desktop elapsed is
inconclusive: unchanged controls vary up to16.5%; all adverse observations remain.
Quiet B504: five jobs142.204s, four independent processes per job, five-second
execution warmups and seven rotated150ms rounds, normal GC/tiering. Included warm
clock: mediated RAM-file I/O, full output checks and prior-result cleanup; excludes
startup, compilation, reset/protocol and final teardown. External process CPU is
separate and coarse. No universal, startup, pending-I/O or CPython speedup claim.

Retain this phase deletion and allocation reduction with modest observed CSV/text
elapsed benefits; JSON/control uncertainty is explicit debt, not a percentage veto.
No second unchanged campaign. Dynamic callback types can allocate owned arrays in
place of the old borrowed pool; this remains a limitation, not a new dispatch project.

60 internal/1636 public focused Release checks pass per actual.NET11/10, including
five new public cases and two independent callback Python goldens. Five complete
scripts also match CPython stdout/file goldens. Covers defaults, variadics, bound
receivers, wide/dynamic/reentrant calls, genuinely pending default lookup, cancellation
through gated cleanup, retained result funding and shared allowance reuse. Existing
refusal/side-effect assertions remain. Full platform/Stress/installed-package checks
batch at this delivery; focused checks do not claim that qualification is complete.

All identity/output/quiet/join/RAM/archive/localSHA gates pass. Finite lease, no
remaining VM users/fixtures, no space recovery/floor changes. Original invalid CSV
catalog transform failed the independent Python syntax gate before timing; original
condition/log retained, corrected catalog revalidated. Publication postponed; broader
whole-script performance and independent regex development remain active.

[Quiet summary](summary.json), [desktop allocation/control summary](local-summary.json),
[validation/source status](delivery-source.json), [Python goldens](python-goldens.json),
[script goldens](script-goldens.json), [terminal receipt](terminal.json),
[raw VM evidence](observations.tgz), [original conditions/source/logs](preparation.tar.xz).
