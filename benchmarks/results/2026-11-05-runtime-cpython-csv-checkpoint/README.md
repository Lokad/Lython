# CPython checkpoint after CSV/list/formatting construction

2026-11-05. Retained source **a8e476bf**, same production in **c6d71108**, frozen
core **402ae9f1**; previous external **0997baa8/68a9f3f8** and an identical-current
control run alongside CPython. Earlier D31–D33 delivery41974563982 is GREEN/all8
gates/cached. D34 final41977587599 subsequently completed GREEN on c6d71108,
all eight Windows/Ubuntu, actual11/10, original stress and installed-consumer gates
([cached receipt](../2026-11-05-private-csv-records/delivery-ci-status.json)).
Publication postponed.

Quiet dedicated Standard_F4as_v7, four AMD EPYC9V45 vCPUs/16GiB/no swap,
Ubuntu24.04.4. .NET11RC3 SDK/runtime11.0.100-rc.3.26522.148/11.0.0-rc.3.26522.148.
CPython3.13.16/GCC13.3 PGO/LTO, ordinary GIL/GC, `-I -S -X utf8`, no JIT/debug.
Existing protocol2, ordinary worker GC/tiering; seven scripts warm/fresh plus
CSV/report compilation.5s warmup per engine/job,7rotated~150ms batches, reversed
roles for later jobs. **439.56s/16jobs**, within540s owned bound. No new framework
or ten-minute iteration lane.5s settled pre-gate preserves original thresholds.

Invocation, mediated RAM-file I/O, full stdout/output checks and prior-result
cleanup are timed. Fresh adds Compile/Dispose; compilation-only excludes execution/
I/O. Reset/staging/startup/hash/final cached teardown excluded; actual users joined.
Every Ready/core/runtime/catalog identity, independent full golden, Closed/exit0,
quiet endpoint, fixture removal and free exclusive lease passes. No actual
suspended I/O, startup/RSS/embedding-latency or individual CPU-cause claim.

| Fresh complete script | Lython ms | CPython ms | Lython / CPython | Control / candidate IQR |
| --- | ---: | ---: | ---: | ---: |
| CSV/native helpers,4096records |5.005|3.497|1.43×|0.25% /0.72%|
| CSV/ordinary helper,4096records |5.943|3.540|1.68×|0.76% /1.80%|
| Text/helpers,3072lines |2.745|1.279|2.15×|0.10% /0.84%|
| JSON-lines/helpers,4096records |4.992–5.182|4.191|1.19–1.24×|3.65% /1.78%|
| JSON-to-Markdown,3072records |3.710–3.928|2.506|1.48–1.57×|5.54% /5.25%|
| Config edit,1024paths |0.267|0.255|1.05×|0.53% /0.89%|
| Long CSV,16records |0.398|2.430|0.16×|1.20% /1.17%|

Warm ratios: nativeCSV1.46×, ordinaryCSV1.71×, text2.17×, JSON1.17–1.24×,
report1.57×, config1.06×, longCSV0.11×. Warm nativeCSV candidate/CPython IQR3.21/
2.53%; warmreport candidateIQR4.38%; warmJSON current-control spread5.23%.
Ranges show identical-source role medians, not confidence intervals. Keep both
JSON and report roles visible; a quiet gate does not guarantee stable timings.

Compile-only CSV69.79µs/54.73µs=1.28×,report51.03µs/37.66µs=1.36×. Candidate
IQR2.58/.86%,controls.62/1.77%. Do not infer compilation by subtracting fresh/warm.
Ordinary-function CSV is a distinct script, not a controlled replacement of only
one call instruction. Its Python baseline uses ordinary helper calls too.

## Coupled source comparison and remaining debt

Current versus0997 text favors current by2.2–2.4%warm and2.9–3.0%fresh, with tight
current controls. Reportwarm favors current~4.0–4.7% but IQR limits magnitude;
freshreport ranges−1.9–7.4% across current roles. NativeCSV and compilation near
unchanged; ordinaryCSV modestly favorable with dispersion limiting small claims.
Configwarm+0.5–1.4% remains visible. These are coupled D31+D33+D34 effects.

WarmJSON's selected candidate is **+5.50%** versus0997 while its identical current
control is−0.01%; freshJSON+1.57% /−2.14%. Those adverse/control-limited points
remain explicit; neither a JSON regression nor improvement is resolved by selecting
the favorable role. No automatic repeat is needed to establish the remaining gap.

The [D34 roughly1% warmtext debt](../2026-11-05-private-csv-records/README.md) remains
accepted at this delivery: the coupled text gain cannot isolate or prove that
component cost fixed. Preserve original+1.9–5.1% and unchanged-bit+0.8–1.0%
conditions. No additive gains, causal CPU attribution or claim of reaching CPython
parity. Complete activation/transport and text work remain priorities; scalar
representation and frontend changes require actual eliminated-work evidence.

[Plan](plan.md), [exact source/binary identity](identity.json),
[all metrics](summary.json), [raw/catalogs/source/cleanup](observations.tgz),
[archive receipt](archive-receipt.json). SHA256
`fbf4cb9113d889976d0da5addc2377331afb9e177192d8404c347091c622ba6c`.
Both Lython sources use identical d497 worker/deps/runtimeconfig copied beside
their exact core, avoiding the preserved B250 executable-symlink failure. No failed
job, timeout or discarded sample in this collection. Earlier source/adverse results
remain at the [0997 checkpoint](../2026-11-05-runtime-cpython-construction-checkpoint/README.md).
