# One executable generator-expression engine

R445/B848, 2026-11-16 UTC. Retain the coherent migration against qualified
`312f5911` / core `0443b5cf`. Prototype core `16c37b02`; exact source inventory
and deleted file are recorded. No public API or regex-provider change.

Remove the 261-line `PyGeneratorExpression` recursive interpreter, its separate
clause/enumerator/outer-attachment lifetimes and runtime syntax lowering. Extend
the existing callable-preparation walk and reuse the existing executable generator
for class suites and annotation consumers. Ordinary executable generators already
use that engine. No runtime compiler, cache, owner or alternate backend is added.

Class-created generators previously lacked Python send/throw/close under both
public hosting APIs. Seven independent CPython goldens now pass. Existing checks
found a class-cell selection difference; capture through the class function
closure corrects it, preserving the original assertions. The outer iterable still
evaluates immediately in the ordinary class namespace; body lookup skips it.

| Complete source / phase | Elapsed change | Wider process CPU change | Reference / candidate process spread |
| --- | ---: | ---: | ---: |
| Class-generator report, compilation | +4.00..+9.43% | +4.21..+8.24% | 1.94 / 3.22% |
| Class-generator report, warm | −9.37..−8.14% | −10.16..−4.75% | 0.47 / 0.87% |
| Class-generator report, compile-to-result | −7.21..−4.14% | −7.32..−2.68% | 3.19 / 0.11% |
| Ordinary normalization control, warm | −2.45..+2.28% | −3.33..+1.70% | 4.78 / 0.07% |
| Ordinary calls control, warm | +0.77..+0.99% | −2.34..0.00% | 0.04 / 0.17% |

Five jobs completed in 152.091 seconds. Same three-case catalog, scripts,
fixture/stdout/file hashes, benchmark worker and normal optional 0.4 adapter;
two independent processes per source, four-second request-wall warmup and five
rotated approximately half-second rounds. Four pairings of medians are ranges,
not confidence intervals. Maximum sample IQR 3.02%. All image/Ready/golden/quiet/
Closed/exit/join/archive gates pass. No workers or RAM fixtures remain.

The report renders 1,024 JSON records (189,441 input bytes) to a complete Markdown
file using a class-created generator. Both revisions run the same source. Warm
includes mediated RAM I/O, complete stdout/file validation and prior-result
cleanup; excludes compilation, startup, fixture reset/protocol and final cache
teardown. Fresh adds compilation and its owner disposal; compilation includes
code construction/disposal. Wider CPU includes reset/protocol. Independent phase
medians are not additive. No new CPython, RSS, pending-I/O or embedding-tail claim.

Local natural-GC whole-process allocation: report 3,381,008 → 2,667,932 bytes/run
(−21.09%); controls effectively flat. Accounted execution peak 3,493,993 →
3,494,889 bytes, because the common frame/cursor is admitted at construction.
Conservative earlier denial and extra compilation work are accepted contract/cost
tradeoffs. Ordinary call elapsed adversity remains; CPU does not corroborate it.
Moving normalization control makes that result uncertain, not a demonstrated loss.
This does not explain the existing ordinary normalization/CPython gap.

Focused Release checks pass each actual .NET 11/10: 191 internal / 957 public
generator, comprehension, lambda, scope, annotation and compilation checks, plus
39 type-scope checks. Overlapping targeted checks are not summed. Independent
pending host cancellation and construction refusal retain outer storage until
awaited disposal and return the shared allowance to baseline. Full platform,
original stress and installed-package delivery qualification is pending.

Original failed fixture and class-cell checks remain in the checks archive. An
initial staging failure listed unused local Allocation harness files as remote
worker dependencies. No guest workers or RAM fixtures had started. Exact partial
image/hash/ownership verification corrected the inventory to the existing 32-file
worker image before measurement; no timing observations were discarded. Two old
working replicas only were retired after committed archive/member equality and
fresh UID/path/hash/inode/all-alias proof. Canonical evidence, current images,
toolchains and 254/252 MiB floors remain. Terminal is cached; never query it again.

[All observations and controls](summary.json), [allocation](allocation-summary.json),
[raw archive](observations.tgz), [exact sources](candidate-sources.tar.xz),
[procedures, checks, initial conditions and core images](procedure-and-checks.tar.xz),
[retention decision](decision.json). Publication/tagging remain held. The larger
whole-script objective stays open after this correctness and consolidation milestone.
