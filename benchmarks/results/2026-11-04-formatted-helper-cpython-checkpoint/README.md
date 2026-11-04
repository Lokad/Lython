# Current formatted-helper CPython checkpoint

Measured2026-11-04: committed `ffc011341f7c5c8ca9e7223f09de174fccacbb05`
production source, frozen core
`718796567fc8e36c597eddd1d92c87b93573b833f3cf69a85ee345aa05686c14`.
The physical B201 image retains its actual48d4bf14+dirty informational identity;
the four-file source snapshot and hashes in the
[B201 evidence](../2026-11-04-native-formatted-returns/README.md) match the commit.
No binary was relabelled or rebuilt for this collection. Comparison worker/protocol2
is unchanged. All Lython roles use identical core bytes.

Quiet Azure Standard_F4as_v7, Ubuntu24.04.4, four AMD EPYC9V45 vCPUs,
16GiB/no swap. .NET11.0.0-rc.3.26522.148/SDK11.0.100-rc.3.26522.148;
CPython3.13.16 GCC13.3 PGO/LTO, ordinary GIL/GC, no JIT/debug, -I -S -X utf8.
Three independently started workers per job: current Lython, identical-source
Lython control, CPython. Five-second warmup per image, one-second settling,
nine rotating approximately200ms samples; preparation roles reverse halfway.
No GC/tiering overrides.12 jobs complete in277.633s; owned service278.261s below
420s bound. Ready identities, all stdout/files, balances, original quiet gates,
normal exits and cleanup/joins pass. No guest fixtures remain.

## Included work and observations

Fresh compile-run includes compilation/disposal plus one complete synchronous
invocation in an already-running host. Warm reuses compiled code. Both include
invocation, actual mediated RAM-file I/O, full checks and prior-result disposal.
Staging/reset, startup, protocol/hash work and final cached-owner teardown are
outside both clocks; all users finish and are joined. Compile-only excludes
execution/I/O. Do not subtract independent phase medians. CPython module caches
persist; Lython execution caches are isolated. No actual suspended-I/O, startup,
isolated CPU, heap/RSS or embedding-latency qualification is claimed.

Ratio=Lython elapsed/CPython elapsed. Control percent=identical-source control
elapsed/current elapsed-1, not an optimization. IQR=interquartile range/median.

| Script / phase | Lython ms | CPython ms | Ratio | Same-source control | Lython / CPython IQR |
| --- | ---: | ---: | ---: | ---: | ---: |
| csv-summary-helpers / warm | 5.119757 | 3.336509 | 1.534 | +0.64% | 0.66% / 0.58% |
| csv-summary-helpers / compile-run | 5.395777 | 3.501735 | 1.541 | -1.96% | 1.41% / 1.79% |
| text-summary-helpers / warm | 3.305417 | 1.160642 | 2.848 | -4.46% | 1.29% / 0.76% |
| text-summary-helpers / compile-run | 3.213352 | 1.288054 | 2.495 | +1.76% | 0.62% / 0.44% |
| json-summary-helpers / warm | 5.289166 | 4.082943 | 1.295 | +2.01% | 0.94% / 0.81% |
| json-summary-helpers / compile-run | 5.525400 | 4.196782 | 1.317 | +0.91% | 0.53% / 0.48% |
| report-render / warm | 4.612447 | 2.387747 | 1.932 | +2.31% | 2.81% / 7.76% |
| report-render / compile-run | 4.835768 | 2.748416 | 1.759 | -0.73% | 3.74% / 1.42% |
| config-edit / warm | 0.199715 | 0.183011 | 1.091 | -0.54% | 0.47% / 0.76% |
| config-edit / compile-run | 0.294996 | 0.251630 | 1.172 | -2.58% | 0.97% / 0.29% |
| csv-summary-helpers / compile | 0.085268 | 0.054195 | 1.573 | +0.41% | 0.83% / 0.47% |
| report-render / compile | 0.060254 | 0.037665 | 1.600 | +2.83% | 1.66% / 0.60% |

Fresh text remains about2.49x CPython; CSV1.54x, JSON1.32x, report1.76x,
config1.17x. **Warm text magnitude is variable:** identical bits differ4.46%,
and its warm median exceeds independently sampled fresh execution. **Warm report
is variable:** CPython IQR7.76%, Lython2.81%; its1.93x ratio is diagnostic.
Fresh report Lython IQR3.74% also limits small differences. Compilation report
control differs2.83%; CSV compilation is steadier. All remain visible; no
confirmation is needed to establish the continuing broad performance gaps.

This is a current external comparison, not a paired experiment against B195 or
proof of any individual optimization. Original adverse B194/B200 async observations
remain independent. The [B201 source pair](../2026-11-04-native-formatted-returns/README.md)
establishes report allocation/elapsed benefits and accepted compilation cost.
The [earlier checkpoint](../2026-11-04-current-cpython-checkpoint/README.md)
retains its actual2d46 source, failed ending gate and justified confirmation.
No percentages from independent conditions are added.

Full runtime delivery for exactffc01134 passes separately:
[cached run41788586642](../2026-11-04-native-formatted-returns/delivery-ci-status.json),
Windows/Ubuntu, actual11/10 ordinary suites, original stress and installed consumers.
Timing evidence and correctness qualification remain distinct.
Publication/tagging remain postponed. The whole-script performance roadmap stays open.

Raw archive SHA256:
`9572b2b171064f42ea9d5f370f23a6630b67db8b840f2b0de17e0bde9487faad`.
`observations.tgz` preserves scripts/manifests, every sample/warmup/response,
Ready/source identities, machine gates, exact workers and prospective conditions.
