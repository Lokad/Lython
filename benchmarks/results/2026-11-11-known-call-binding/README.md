# Compiler-known argument binding — 2026-11-11

Both prototypes are **withdrawn**. Runtime source remains fully qualified
`6904093e`; no prepared argument maps, compiler metadata, identity checks or
associated admission remain. Useful public defaults/order/lifetime regressions
survive. Internal tests enforcing the withdrawn architecture are archived only.

The existing binder consumed compiler-known operand-to-parameter mappings, guarded
by actual immutable signature identity. Defaults remained live reads; dynamic,
variadic and protocol cases kept ordinary binding. The refinement removed unused
completed-binding presence writes and a duplicate entry cancellation check.
Neither form introduced a runtime cache, owner or execution route.

## Decision

Runtime allocation is effectively flat. Compilation allocation increases
0.38–0.54%. The limited gains do not justify this additional metadata and guarded
path: synchronous search remains adverse and asynchronous search loses the first
form's clear direction. This is a decision about these implementations, not a
permanent veto on call/frame redesign or a fixed percentage threshold.

| Quiet complete script | Initial elapsed change | Refined elapsed change | Refined wider CPU change |
| --- | ---: | ---: | ---: |
| Keyword-default search / Run | +0.34..+7.86% | +1.83..+4.93% | −2.32..+1.69% |
| CSV validation / Run | −1.30..+0.51% | −2.95..−0.61% | −6.25..−2.94% |
| Keyword-default search / RunAsync | −7.46..−4.47% | −3.97..+0.94% | −5.64..+4.96% |
| CSV validation / RunAsync | −0.95..+0.55% | −1.41..−0.41% | −6.45..0% |
| Ordinary text control / Run | −3.00..−0.35% | −1.79..+3.57% | 0..+2.80% |

Ranges cover all four independent process-median pairings, not confidence
intervals. Negative means faster. Refined identical-control spreads are
1.50/1.47% for synchronous search and 1.65/3.29% for asynchronous search;
search CPU does not establish a CPU regression. Full control spreads and IQRs
remain in the summaries. Desktop search is adverse with unstable unchanged
controls; all six scripts, both APIs and separate compilation records survive.
The refined desktop compilation slowdown for captured text is retained too.

## Validation and measurement boundary

Initial focused checks total 5,920 across actual .NET 11/10, with additional
overlapping final cases; refined checks total 5,936. Fifteen independent
CPython 3.13.2 correctness goldens pass. Coverage includes argument effects,
rebinding, mutable defaults, receivers, widths 16/17/65, aliases, shared allowance,
actual pending I/O and joined cancellation, and terminal admission refusal despite
guest suppression/finally return. After exact source restoration, a nonincremental
Release build and the 18 retained public regressions pass on each target.

Existing Windows complete-job tooling uses the same worker/adapter, three
isolated core images, 256 warmups and five rotating rounds of sixteen jobs.
Reset, mediated I/O, validation and result disposal are included; compilation is
separate. Desktop clocks remain diagnostic.

B586/B587 use the existing F4as_v7 Ubuntu quiet VM and fixed `fec79` worker,
five-second warmups and seven rotated 150 ms rounds. Five jobs finish in 142.950 s
and 143.084 s. Warm includes mediated RAM I/O, complete validation and prior-result
cleanup; compilation, startup, reset, protocol and final caches are excluded.
External process CPU is wider and includes protocol/reset. No desktop/VM clock
merging, new CPython ratios, RSS or embedding-latency claims.

Both collections pass identities, goldens, balances, quiet gates, joined-user,
RAM-cleanup and independently verified archive checks. Terminal receipts are
cached. Staging-space refusals and recoveries remain in the raw evidence;
retirement used fresh path/UID/inode/all-alias checks against canonical committed
Git evidence, protecting current roles and both prototypes during collection.

[Decision](decision.json), [archive hashes](archive-identity.json),
[initial quiet summary](initial/quiet/summary.json),
[refined quiet summary](refined/quiet/summary.json), and both raw archives
([initial](initial/quiet/observations.tgz), [refined](refined/quiet/observations.tgz))
retain exact source pairs, actual-target cores, failed fixture/preparation
conditions and adverse observations. Full platform/package qualification belongs
to retained `6904093e`, not either withdrawn prototype. Publication is postponed.
