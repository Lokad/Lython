# Abstract-state fork and join shape

An instrumented Windows .NET 11 Release build from `c67b28f80`
compiles each B1289 script once through the public engine. Each compile
returns the borrowed allowance to baseline. The probe counts map
entries and joins. It makes no allocation, elapsed or CPU claim.

| Script | Forks | Copied value facts | Copied length facts | Joins | Paired value facts | Same scalar backing |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Postponed factories | 8 | 9 | 0 | 1 | 1 | 1 |
| Inherited dataclasses | 99 | 197 | 1 | 0 | 0 | 0 |
| 96-field validator | 193 | 386 | 1 | 192 | 384 | 0 |
| Dense scalar control | 147 | 36,960 | 0 | 96 | 24,624 | 24,528 |

The validator copies two value facts per fork on average. Half of its
paired values share backing, and those shared values are outside the
probe's conservative scalar set. Inherited dataclasses make no joins.
These observations weaken the case for a general branch mutation log on
the representative scripts. The synthetic dense control has 24,528
unchanged scalar pairs out of 24,624. It remains appropriate for
detecting growth in branch cost.

[Counts](counts.json) · [Probe and exact catalog](probe.tar.xz).
