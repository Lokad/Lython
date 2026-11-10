# Framed native-entry deletion: withdrawn

This assessment tested whether the existing post-frame native-return shortcut
could be deleted while preserving the primary borrowed-input shortcut. It removed
23 lines across three runtime files above `fcf018d2` (qualified production
equivalent to `cac4acf8`). **The deletion was withdrawn.** It saved no measurable
allocation and slowed the affected complete script on the quiet VM.

The text-summary script was enclosed in one active exception handler; its input,
output and file goldens were unchanged. Four maintained controls and the existing
comparison protocol were reused. The canonical case ID is required by that driver;
this modified source is not the published ordinary text-summary measurement.

| Affected text script | Elapsed change | Wider process CPU change |
| --- | --- | --- |
| Synchronous API | +5.28–9.66% | +5.08–8.62% |
| Asynchronous API | +12.47–15.98% | +13.46% |

Quiet B521 took 141.364 seconds for five jobs, four processes/two per source,
five-second role warmups and seven rotated 150 ms rounds. Allocation was flat.
The synchronous reference/candidate process spreads were 1.99%/2.08%; asynchronous
spreads were 1.74%/1.36%. Unchanged controls include adverse or inconclusive results:
report reference spread 4.68%, Unicode candidate spread 6.70%. No universal CPU
claim or attribution of the entire change to instruction dispatch follows.

The boundary includes mediated RAM I/O, complete validation and prior-result
cleanup, excluding process startup, fixture reset, protocol work and final caches.
Process CPU covers a wider interval. Compilation is separate. This is a Lython
revision comparison, not a CPython baseline. See [quiet summary](quiet/summary.json),
[identities](identity.json) and the [owned archive](quiet/observations.tgz) for exact
sources, machine/toolchain and observations. The archive receipt records its SHA-256
`82b084b6b0a815eca609fc21a4fc4124c0eacf4d596910218f08118b855714a0`.

All output, balance, quiet-host, worker-join, RAM-unmount, owned-archive and local
SHA checks passed. Terminal status is cached; no workers or fixtures remain.
Focused Release checks passed 369 internal and 96 public cases on each actual
.NET 11/.NET 10 target. Three semantic scripts also agree with isolated CPython
3.13.2. Full platform/package qualification was not repeated for withdrawn code.

Original desktop observations conflict: affected synchronous elapsed improved
23–34%, asynchronous elapsed worsened 5–13%, and controls moved substantially.
They remain diagnostic in the raw lanes. An original six-case setup was rejected
before timing; its catalog and stderr are retained. The corrected five-case catalog
and source hashes identify the measured run. Exact redundant compressed-copy
recovery receipts are retained; canonical evidence and frozen binaries survive.

[Source/check archive](source-checks.tar.xz), [patch](source.patch), qualification
logs and [withdrawal decision](withdrawal.json) preserve the assessed source and
conditions. Production was restored exactly and freshly built. The three public
regressions remain useful. A different shared-kernel design remains open; deleting
this shortcut alone is not a simplification worth retaining.
