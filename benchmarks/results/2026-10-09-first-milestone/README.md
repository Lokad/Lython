# First consolidated improvement milestone

The reference and candidate warm lanes completed in **5m58s and 6m28s**, a total
of **12m26s**. Each had a 600-second whole-group cap, one attempt, the same v6
harness and a predetermined 14-case profile. This milestone follows the accepted
synchronous dispatcher, exact integer addition and bound loop-name improvements.
Ordinary improvement rounds continue to use seconds-long microbenchmarks.

Both empty/tiny controls qualified on both producers. **No workload qualified on
both producers**, so this milestone does not certify an old/new improvement ratio.
The basic-loop sessions still have an evidence failure, described below. Its
absolute medians support the earlier short-run trend but remain diagnostic:

| Large loop, 16,384 iterations | Lython session medians, ms | CPython session medians, ms | Status |
| --- | --- | --- | --- |
| Reference | 3.601 / 3.702 / 3.874 | 0.577 / 0.601 / 0.776 | Unqualified |
| Candidate | 2.325 / 2.372 / 2.189 | 0.621 / 0.615 / 0.578 | Unqualified |

The reference's keyword-call workload qualified; the candidate's ASCII pipeline
and zlib application qualified. Their CPython/Lython median ratios and paired
95% intervals are shown separately for each independent session:

| Producer and qualified workload | Session 1 | Session 2 | Session 3 |
| --- | --- | --- | --- |
| Reference: keyword calls | 0.099 [0.098, 0.099] | 0.104 [0.104, 0.105] | 0.103 [0.103, 0.104] |
| Candidate: ASCII pipeline | 0.035 [0.033, 0.035] | 0.032 [0.032, 0.034] | 0.033 [0.031, 0.034] |
| Candidate: zlib application | 0.107 [0.107, 0.108] | 0.108 [0.107, 0.109] | 0.108 [0.107, 0.109] |

These qualified jobs favor CPython. The zlib job measures its complete application
and library integration. It is not a measurement of interpreter dispatch alone.
There is no overall speedup, pooled interval, control subtraction or memory ratio.
The ASCII pipeline is a concrete target for a subsequent short improvement round.

The complete generated tables retain every session median and exclusion:
[reference](reference-warm.md), [candidate](candidate-warm.md). The reference
stopped stable-sort and tuple-key update cases at idle gates; the candidate stopped
tuple-key updates. Remaining exclusions include preparation evidence, timer floors,
spread/order/interval/session checks and the execution control floor. They are not
recollected or relabelled.

Review found **14 reference and 12 candidate preparation pauses below two seconds**,
typically around 1.999 seconds. The implementation used `Task.Delay(2s)`; the
captured monotonic interval sometimes fell short of the declared minimum.
Specifically, reference large-loop session 3 recorded 1.999478132 seconds and
candidate session 2 recorded 1.999544890 seconds. Other sessions passed, but all
three are required. These failures remain excluded under the frozen v6 verifier.
Separately, two reference and four candidate complete sessions included measured
batches below the 20 ms floor; those exclusions also remain.

The follow-up collector rechecks the monotonic clock and waits again after early
wakeups, rounding a positive remainder up to a whole millisecond to avoid spinning.
The minimum pause, eligibility rules and deadlines stay unchanged. Deterministic
early-wake and cancellation tests plus a three-millisecond real-clock check cover
this defect. This implementation correction applies to future collections; the
milestone tables were rendered with their original frozen harness.

The reference producer is `be8361779e59c0ce873f768bdbabb668f6100c55`, on
`bench/milestone-reference-v6-20261009`. Its production `src` tree is identical to
pre-improvement `56d4346d`: `33c91d8d8901d29632af325d3ff7273cbc33f6b3`.
The candidate is `4fb489d97783aab6cadc94387661f60378f689c5`, production tree
`22668d84b6209a539ae6bf0be3d4e56d361042c9`. Both benchmark trees are exactly
`a9a4df1af49132e00a8da4a293fbe3801702ec86`. The same 14-case catalog SHA-256 is
`e6cf197948d903c59d4db5e940d9ca50433cbb0c6399ab999817f48775fa4fe1`.
The declaration, source trees, catalog and artifact hashes were frozen before
collection. The reference ran first, then the candidate; this was not an old/new
paired experiment. Correctness fixes are included in the candidate's source.

Both clean Release producers used SDK 10.0.401/runtime 10.0.12 and ordinary optimized
isolated CPython 3.13.16. Each passed 370 comparison-related public checks on the VM;
the reference also passed 370 locally. Both frozen heads passed Windows and Ubuntu
CI, including probes, full Release suites and package consumers:
[reference CI](https://github.com/Lokad/Lython/actions/runs/37962634566),
[candidate CI](https://github.com/Lokad/Lython/actions/runs/37959393573).

The independent offline audit checked **1,119 reference and 1,206 candidate
successful responses**, identities, complete goldens, counts, timing formulas and
absolute medians. Every timed input was physically rehashed after both lanes.
Actual service configuration proved the caps and cleanup settings; workers were
absent, services stopped, both checkouts stayed clean and the lease was free.
Observer commands only tracked the owned service; VM builds, tests and transfers
finished before timing, and rendering happened afterward.

| Artifact | SHA-256 |
| --- | --- |
| Reference receipt | `0c1251e615f8475ee65002e307a5c2c0a45ec6d47f00ec730ec3f12672bcf0cd` |
| Candidate receipt | `1905db85faad501f45366f1d087231f0ed12b27f7fcd827832b7f1a0f1624f60` |
| Reference rendered report | `dfec739abea7ebc207d554a30b6004aedf6189d3d342eee9480302f37fa87107` |
| Candidate rendered report | `701eed1ded4017dd2cb5bddea29d54dfe18264c6eda0ca1a55f7241fcabe8ffb` |
| Independent audit.py | `25ed60ed42ad5fa2aafe562e4dff415790168ec9d8d4bb47307960ada793461b` |

Raw receipts, all attempts, setup-failure notes, declaration, service configuration,
source/input checks, scripts, tests, original rendered bytes and audits remain under
the ignored `.git/agent-notes/first-milestone-20261009/` directory. No subsequent
full lane is run to obtain a favorable result. Matched old/new and basic-loop
qualification remain pending a future declared milestone.

Report hashes refer to the original Linux-rendered bytes preserved in those notes.
Checkout line-ending conversion may change a local Markdown file's byte hash.
