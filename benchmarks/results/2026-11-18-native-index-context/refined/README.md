# Explicit indexing context: changed-source refinement

R523/B954,2026-11-18UTC. **Private, unpromoted0284157e** versus qualified3f873227 /
a5038a28. Same four jobs/8s warmups/five rounds,187.89s. Scope correction keeps
ordinary CopyFromUtf8's existing ambient check and default long-copy behavior;
only explicit native callers carry context to construction. Native string count/
cache/copy remains explicit, removing the native ambient scope. No new framework,
policy/cache/owner. Bothactual11/10 pass59internal/144public,13goldens/allocation/
reuse/exact4timedgoldens. Originalb676adversity remains [separate](../initial/README.md).

| Complete script | Phase | Elapsed change | Wider CPU change | Reference / candidate spread |
| --- | --- | ---: | ---: | ---: |
| Floating JSON report | Warm | −2.14..+0.53% | −1.21..+1.51% | 2.18 / 0.55% |
| Same | Compile-to-result | −4.69..−3.19% | −5.44..−3.75% | 0.90 / 0.67% |
| Helper text | Warm | −0.36..+1.73% | +0.24..+1.95% | 0.86 / 1.23% |
| Positional calls | Warm | −0.39..+4.06% | −1.28..+2.97% | 0.73 / **3.71%** |

Fresh floating benefits have supporting CPU; fresh IQR3.19% and all samples stay.
Warm floating is inconclusive; helper CPU is modestly adverse. Positional candidate
process controls disagree despite stable within-process IQR. One bounded unchanged-
image fresh/call confirmation with12s warmups/opposite roles is justified. Do not
infer a causal recovery by subtracting initial and refined collections. Allocation
is effectively flat, accounted peaks unchanged;13allowance baselines restore.
No third full catalog collection, CPython ratio or universal gain. Retention/debt/
refinement/withdrawal remains pending usable controls and simplification value.

Guest warm includes Run, mediated RAM I/O/goldens/prior-result cleanup; fresh adds
compile/compiled-owner disposal. Startup/reset/protocol/final caches excluded,
process CPU wider. All32image/catalog/quiet/Closed/exit/join/SHAarchive gates pass
before RAM teardown. Only exact archived initialb676core replica retired after
fresh all-path/hash/UID/link proof; current images/floors preserved. The local
allocation helper refused to overwrite original observations before doing work;
original output/failure preserved and remaining checks completed into a fresh path.
Publication held; broader objective remains active. No qualification inherited.

[Summary](summary.json),[raw](observations.tgz),[identity](identity.json),
[source](candidate-sources.tar.xz),[patch](prototype.patch.gz),
[procedures](procedures-and-checks.tar.xz).
