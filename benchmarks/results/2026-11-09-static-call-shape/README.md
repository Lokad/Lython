# Shared static call syntax

R133/B494, 2026-11-09. Reference c343687b/core95fc versus candidate36fd.
Static analysis shares one positional/keyword expression partition for each
ordinary call. Both concrete-argument queries use that syntax fact rather than
rebuilding lists, dictionaries and parallel inferred-value containers. Current
binding values are resolved on demand; expanded calls still rebuild from current
bindings. No runtime binder, owner, public API or admission-estimate change.
The existing conservative compilation reservation covers retained syntax facts.

**Retain for construction removal and allocation reduction.** Actual compilation
allocation falls 2.59–4.14% across five complete CSV/text/JSON/report/config scripts;
warm allocation varies within0.081%. No demonstrated general CPU improvement.
47 internal and206 public focused cases pass on each actual.NET11/.NET10 target.
Three new checks preserve current-state values across clones/rebinding and expansion
that is initially unresolved. Existing semantic/assertion/timeout coverage remains.

Desktop elapsed is diagnostic: configcompile+15.74..+17.70%, asyncJSON+12.83..+32.86%
and unchanged control spread up30.09% remain in the raw results. Quiet F4as_v7/
Ubuntu24.04.4/.NET11RC3, normalGC/tiering, same frozen workers/catalog/goldens,
four independent process roles,5swarmup/role/job,7rotated150msrounds:

| Complete script / boundary | Elapsed change | Wider process CPU change | Reference / candidate spread |
| --- | ---: | ---: | ---: |
| Validated CSV / compile | -2.33..+0.25% | -1.90..-1.16% |1.17% /1.43% |
| Text helpers / compile | -1.41..+0.32% | -1.95..+0.28% |1.66% /0.10% |
| JSON helpers / compile | -3.41..-0.59% | -1.97..+5.76% |0.82% /2.03% |
| Report / compile | -0.01..+0.62% | -0.37..+6.87% |0.46% /0.16% |
| Config / compile | -2.09..+0.04% | -1.87..+0.11% |1.66% /0.48% |
| Config / compile-to-result | -1.17..+2.33% | +3.17..+5.14% |0.19% /3.34% |
| Config / warm control | -0.58..+1.69% | -3.45..+4.65% |1.50% /0.77% |

Elapsed comparisons use both candidate and both reference processes. Config's
large desktop compile adverse result does not repeat; completeconfig remains
inconclusive with candidate process spread3.34% and wider processCPU possible debt.
Report/JSON coarse processCPU also remains uncertain. CPU includes fixture reset,
protocol and validation beyond the execution clocks; do not attribute it entirely
to guest execution. Compile/warm/complete boundaries are separate; complete/warm
include mediated RAM-file I/O, validation and prior-result cleanup, while startup,
reset, protocol and final cache teardown are outside the execution clock.
No CPython refresh or later-HEAD relabeling accompanies this source comparison.

Seven jobs196.114s under a240s finite service. Every source/core/auxiliary/golden/
balance/quiet/Closed/join/RAM-unmount/archive-under-lease/local-SHA gate passes.
Archive SHAa3e6a42d8fd1ee9a65345de2697e7c1fde3c307e66a838ba5f4a4a28a5e66ec3.
Fresh idle exact-byte recovery linked four duplicate Roslyn files:28,049,408B freed,
all original paths/bytes/ownership/modes/inode aliases proved before mutation and
verified after it. Floors/headroom unchanged; original evidence is preserved.
Terminal cached, no users/fixtures. Full candidate platform/stress/package delivery
is a later coherent checkpoint; prior c343 CI is independent. Publication postponed.

[Identity](identity.json), [selection before edits](selection.md),
[focused validation](focused-validation.json), [compile allocation](compile-assessment.json),
[warm diagnostics](warm-assessment.json), [quiet comparison](assessment.json),
[terminal receipt](terminal.json), [space proof](space-proof.json),
[original quiet observations/both exact cores](observations.tgz),
[source, local diagnostics, failed/adverse conditions and preparation](source-and-local-diagnostics.tar.xz).
