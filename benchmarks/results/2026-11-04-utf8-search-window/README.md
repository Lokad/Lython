# Shared UTF8 string search windows

2026-11-04. Existing StringSearchBounds carries one normalized byte window and
validity through all search and prefix/suffix paths. Default windows use the
logical byte extent; explicit bounds normalize once after their hooks. Tuple
affixes reuse the window. Derive scalar coordinates for actual index results.
Remove repeated scalar→byte conversions and redundant empty-range branches:
**59 net production lines deleted**, four files. No new dispatch/cache/arity
family/owner/option; actual indexing caches retain admission and ownership.
Repeated count matches reuse the existing cooperative structural checkpoint.

Correct two Python differences: empty searches starting beyond length previously
returned clamped matches/count1; invalid start bounds previously invoked an end
hook before rejecting. Both new public cases fail on saved7cf2623c and pass through
both APIs on each target. Independent13,608 result/error combinations match
CPython3.13.2 per API/actual11/10, including Unicode/NUL/empty/negative/huge ranges.
Existing50 internal/350 public per actual11/10 pass, plus bounded-search and cache/
ownership selections (overlap possible). Genuine suspended bounds, tuple effects,
terminal refusal and funded cleanup remain covered. [Validation](validation.json),
[sources, original RED logs, matrix and all focused checks](source-and-validation.tgz).

## Original controlled whole-script observations

Baseline65e0871d/frozen7cf2623c, focused/measurement-qualified (full runtime delivery
pending); candidate same source plus archived shared-window patch,frozen1dcca6f2.
[Exact identities](candidate-identity.json). Same quiet AzureF4as_v7/Ubuntu24.04.4,
.NET11RC3, ordinary GC/tiering, identical auxiliary bits/CoreLib/hash witnesses.

| Complete workload / phase | Candidate elapsed | Change vs both old roles | Same-source spread | Candidate IQR |
| --- | ---: | ---: | ---: | ---: |
| ASCII long-line prefix/count / warm;262,144B |0.2646ms|−21.98% /−21.96%|0.02%|1.76%|
| Unicode long-line prefix/count / warm;263,168B |0.2634ms|−23.48% /−23.48%|0.00%|0.67%|
| CSV helpers / warm;4,096 records |5.1678ms|+3.25% /+1.63%|1.60%|0.35%|
| Text helpers / warm;3,072 lines |3.2104ms|+0.45% /+1.72%|1.25%|3.70%|
| JSON-to-Markdown / fresh;3,072 records |4.9452ms|+0.21% /+0.22%|0.01%|0.63%|
| Config edit / fresh;1,024 paths |0.2987ms|+1.49% /+0.78%|0.71%|0.32%|
| CSV helpers / compilation |88.046µs|+2.35% /+3.32%|0.94%|2.57%|

Negative means shorter elapsed; allocation essentially unchanged. All identities,
full CPython3.13.16 goldens, owner baselines/final zero, fixture cleanup and before/
after quiet gates pass.207s normal owned collection below300s; all users joined,
service inactive/MainPID0 and lease free. [Original plan](pre-measurement-plan.md),
[metrics](assessment.json), [raw observations](observations.tgz), SHA256
`340bb70727fc35081b64582f9ac9890bd81b41ed32d17df9ba31c2c23f7d7084`.

## Bounded reversed-role confirmation and decision

Common small increases, including unchanged compilation, justify one role-
sensitivity confirmation. Swap physical images: before/before-control nowNEW
1dcca6f2, candidate roleOLD7cf2623c. Preserve exact images and sampling policy.
Four cases only;119s, all gates/owners/goldens/cleanup pass, below180s.

| Work | NEW elapsed change vs OLD | NEW same-bits spread | Interpretation |
| --- | ---: | ---: | --- |
| Unicode prefix/count |−21.74% /−22.97%|1.57%|Affected improvement persists|
| CSV warm |+3.57% /+0.57%|2.90%|Possible small regression; magnitude control-limited|
| Config fresh |−0.47% /−1.92%|1.46%|Original adverse direction reverses|
| CSV compilation |+0.98% /−0.86%|1.82%|Within dispersion; original regression unconfirmed|

[Plan](confirmation/pre-measurement-plan.md), [explicit NEW/OLD interpretation and
raw label metrics](confirmation/assessment.json), [all confirmation observations](confirmation/observations.tgz),
SHA256 `cabad80889c88d0da881390749c7a97d6d5b8dfa0edc16f5f93d770cac5cd68a`.

Retain coherent deletion, two correctness fixes and affected-script improvements.
Accept possibleCSVwarm0.6–3.6% regression debt with control-limited magnitude;
text movement remains uncertain. Compilation is not a demonstrated frontend
regression. Preserve every original/adverse result; stop retiming this source pair.
No broad CPU, RSS/embedding-latency gain, additive earlier improvement or updated
CPython ratio. Subsequent external checkpoint and full platform/Stress/installed
consumers qualify this coherent runtime delivery independently. Publication held.

Both collections use the existing private three-image driver:6sec guest warmup/
image, alternating preparation,2sec settle,9rotated200ms baseline-sized batches.
Actual mediated RAM-file I/O, full output/file checks and result cleanup included;
fixture reset/staging/startup/hashing/final cached teardown excluded. Compile
separate; fresh includes compile/dispose. Targeted scripts filter256 lines of1KiB
by shared prefix tuple and count tokens. No cached-string-only microbenchmark.
