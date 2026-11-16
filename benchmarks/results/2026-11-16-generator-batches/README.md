# Generator bulk consumption: initial placement under refinement

R458/B861–B862,2026-11-16UTC. Qualified9e8d1cf5/e92d9598 versus private82e45812
plus archived prototype99e6554d. Production remains unchanged. The prototype
shares Advance/AdvanceAsync across single pulls and admitted-capacity native drains;
its initial placement is **not promoted**. A changed-source refinement is active.
Publication remains held.

| Complete workload | Phase | Initial elapsed change | Longer-warmup confirmation |
| --- | --- | ---: | ---: |
| Text normalization,4,096lines/52,224B | Warm | −10.00..−3.46% | −12.13..−4.20% |
| Same normalization | Compile and run once | −14.84..−10.79% | Not repeated |
| Ordinary generator/list/render control,16,384values | Warm | −0.07..+6.29% | +1.35..+9.26% |
| Same ordinary generator control | Compile and run once | −3.55..+5.62% | +0.37..+5.72% |
| JSON-to-report control,1,024records | Warm | −2.40..+1.10% | Not repeated |
| Numeric report control,4,096records | Warm | −1.87..+1.83% | Not repeated |
| CSV invoice control,4,096rows | Warm | −4.77..−0.29% | Not repeated |

Ranges retain all four pairings of two process medians per source. Negative means
candidate faster. Normalization CPU is favorable in both collections. However,
ordinary-generator CPU is adverse in confirmation,+1.20..+9.50%warm and0..+6.43%
fresh, with moving controls. This needs correction or an explicit regression-debt
decision; it is not an unchanged-source speedup claim. Warm reference/candidate
spreads are5.51/1.67%initial normalization,3.11/5.74%confirmation; ordinary-generator
confirmation spreads2.06/5.63%warm and4.94/0.37%fresh. Allocation and accounted peaks
are effectively flat; there is no memory-reduction claim.

The list(values()) control uses its existing admitted array construction, which
bypasses DrainLeased. Its classification as an unaffected ordinary-generator
control was recorded before observing the results. Normalization is the affected
complete script. All seven frozen worker scripts agree with isolated CPython3.13
on outputs/files; this is semantic verification, **not a new CPython timing**.

Quiet AzureF4as_v7 Ubuntu24.04.4,four EPYC9V45vCPUs,16GiB/no swap,.NET11RC3,
normal tiering/workstation GC. B861 seven jobs210.293s uses4s full-request warmup;
B862 three jobs140.823s uses8s warmup to examine moving controls. Both use five
rotated~0.5s samples/process and the same frozen sources/images/catalog. Warm
includes Run, mediated RAM-file I/O, all stdout/file goldens and prior-result
cleanup; compilation/startup/reset/protocol/final-cache teardown excluded. Fresh
adds compilation and compiled-owner disposal. Wider CPU includes reset/protocol.
First-verification clocks include one compile/two runs/protocol, not isolated startup.
No RSS/heap/embedding-tail claim. All quiet/Closed/exit/join/archive gates pass;
SHA-verified persistent evidence precedes fixture teardown, no users remain and
terminal observations are cached permanently. No unchanged retiming is pending.

Focused actual Windows.NET11/10 each164public/59internal pass, plus183additional
public.NET11 and57lifetime.NET10 cases (overlap not summed). Six later public rows
per target check exception state, reentrancy, bounded unpacking, aliases and terminal
refusal/budget reuse; earlier four semantic programs match CPython independently.
Initial source archive has four rows, postfreeze test archive has six. Full platform,
stress and installed-package qualification is deliberately not claimed.

[Initial results](summary.json),[confirmation](confirmation/summary.json),
[allocation](allocation-summary.json),[validation](validation.json),
[frozen source/image identity](identity.json),[source](candidate-sources.tar.xz),
[raw initial](observations.tgz),[raw confirmation](confirmation/observations.tgz),
[focused logs](focused-checks.tgz),[procedures/custody](procedures.tgz),
[changed-source refinement design](refinement-design.md).
