# Shared constructor sequence and operands — 2026-11-06

R09 passes ordinary positional class arguments from existing operands to final
function locals. The sealed object allocator needs no class-prefixed copy; user
allocators and initializers bind class/self directly. Compiled bound functions
use the same receiver entry as lowered functions. One construction sequence
replaces duplicate synchronous/asynchronous drivers; common operand entry retains
managed call admission and exception translation. Dynamic call forms keep their
general binder and admitted owned copies. No cache, pool, new option or backend.

Correct four confirmed Python gaps: a returned subtype chooses its own initializer;
staticmethod, classmethod and descriptor initializers bind correctly. Resolve init
after allocation/mutation, skip it for unrelated returns, and await actual descriptor/
body host I/O. First refused admission/cancellation stops later hooks even when a
native callback returns an otherwise valid object/None. Borrowed operands and the
new instance remain funded until all users/cleanup finish; escaping results retain
their owner.

[Contract](contract.md), [identity](identity.json), [validation](validation.json),
[ten isolated Python goldens](goldens.json), [focused logs](focused-checks.tgz).
Original failing probe outputs remain next to corrected results. Focused **5,698
public /915 internal per actual.NET11RC3/.NET10.0.8** pass. New16public/10internal
cases cover argument rebinding, genuine pending allocator/descriptor/initializer
I/O, retained large aliases/results, refusal/cancellation and shared-budget reuse.
Final local.NET11 totals **4,249 internal /16,176 public** pass.

## B341 complete scripts

Frozen candidate **b909322b**, exact five source hashes/patch on5563ccdd;
qualifiedR06 reference **a2a61290/core5c0283e1**, verified against installed package
02749D69. Source was built before receipt-only5563 commit; source and generated
assembly metadata are distinguished. Unchanged b94 worker/protocol2, quietF4as_v7,
.NET11RC3/CPython3.13.16, ordinary GC/tiering. Two independent identical current
processes, reference and CPython; five-second warmup/role/job, seven rotated150ms
rounds. **113.58s /4jobs**, all identity/golden/quiet/Closed/funding/join/fixture
gates pass. [Raw archive](observations.tgz), [hash/join receipt](archive-receipt.json),
[all medians/IQRs](summary.json), [paired assessment](assessment.json).

| Whole script / boundary | Candidate / identical control | Qualified R06 | Change |
| --- | ---: | ---: | ---: |
| CSV class records, warm |9.378 /9.308ms|9.603ms|−2.35 /−3.08%|
| CSV class records, compile-to-result |9.611 /9.649ms|9.811ms|−2.04 /−1.65%|
| Unchanged original CSV, warm |4.669 /4.685ms|4.767ms|−2.05 /−1.73%|
| Unchanged text/helpers, warm |2.318 /2.301ms|2.301ms|+0.745 /−0.012%|

Current class-role spread0.75% warm/0.39% fresh; IQR0.91%/1.73%. CSV control
improves similarly, so timings do not establish a constructor-specific CPU gain.
Text straddles the reference. **Retain** the shared convention, removed packaging
and Python corrections, with modest diagnostic elapsed observations. No allocation
percentage, broad speedup, RSS or embedding-latency claim. Do not compound these
source pairs with R06/R07 or declare earlier debt paid. The whole-script objective
remains open; the primary guide still identifies its own measured4ace68ff source.

Mediated RAM I/O, complete output/file validation and prior-result cleanup are in
execution clocks; fresh adds compilation/disposal. Startup/staging/reset/transport/
final teardown are outside. Actual pending I/O is tested separately, not timed as
latency. No live measurement remains.

## Qualification correction / contract tradeoff

The first full local run passed all16,176public and4,245internal cases; four old
allocator fixtures failed because they pinned all remaining allowance and assumed
argument transport was uncharged. [Original full log](full-net11-first.log) is
preserved. Dynamic allocator input copies are now admitted/owned, even when the
allocator returns an alias or foreign result. Those copies conservatively remain
charged to the execution/result owner. Earlier admission refusal is intentional;
identical tiny-budget thresholds are not a contract.

The fixtures now admit argument transport with headroom **smaller than one instance
shell**, preserving detection of an unwanted discarded placeholder and returned
identity. They do not relax timeouts or containment. Both-runtime30targeted checks
pass; the complete internal suite rerun passes4,249. The unchanged already-green
public suite was not replayed. Production source hashes remain those measured in
B341; fixture/generated-metadata changes do not imply new measured bytes.

Final platform, originalStress and installed-consumer qualification batch at
delivery. R06's closed receipt excludes this source. Publication stays postponed.

Committed **933a7fff**. Both actual [installed Release consumers](package-consumer.log)
pass; the exact package hash is in validation.json. CI42201490733 is running
independently for final platform/runtime/originalStress/package gates. Later R10
work is excluded. No publication or unchanged full-suite replay.
