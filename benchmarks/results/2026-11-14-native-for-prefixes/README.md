# Native builtin for-loop prefixes

2026-11-14, R345, qualified `2f606519`/frozen `53becdb6` versus prototype
`8306699f`. Two production files remove whole-function exclusion for GetIter,
ForNext and unprotected loop exits. Existing builtin cursors and final native
slots replace forced ordinary routing; custom iteration promotes before acquisition,
and active cursors/children transfer at the exact instruction without replay.
No new engine, pool, policy, public option or dependency is introduced.

| Complete workload | Allocation change | Elapsed change | Wider CPU change |
| --- | --- | --- | --- |
| CSV digit validation/report, text input, warm | −25.31% | −20.25..−14.71% | −20.90..−14.94% |
| Same script, compile-to-result confirmation | Not measured in this boundary | −19.94..−17.84% | −19.43..−17.40% |
| CSV digit validation/report, list input, warm | +4.24% | −11.01..−7.55% | −10.37..−7.48% |
| Captured text control, warm confirmation | Flat | +0.49..+2.59% | +0.61..+3.99% |
| Fulfillment control, warm confirmation | Flat | −3.31..+4.31% | −2.84..+4.74% |

Changes compare all fresh-process source pairings; positive means more/slower.
The complete4096-row CSV task reads codes, sums their digits with an ordinary
for helper, aggregates groups and writes/verifies a report. List input includes
constructing the list before the same helper call. Both revisions use identical
script/input/output bytes; isolated CPython3.13.2 validates stdout/files. This is
not a CPython timing comparison and not a timing comparison against the historical
while-based digit algorithm. Text-input allocation falls6,343,874→4,738,096B/job;
list-input rises6,180,202→6,442,277B/job. Allocation is a separate four-process
desktop diagnostic, with no elapsed claim.

B714 uses quiet Azure F4as_v7/Ubuntu24.04.4/.NET11, unchanged B685 worker/optional0.4
adapter, ordinary GC/tiering,12s full-request warmups and five rotating1s batches.
Warm includes mediated RAM-fixture I/O, output/file checks and prior-result cleanup.
Compile-to-result adds compilation/script disposal; startup, protocol/reset and final
cache shutdown are excluded. Wider CPU includes protocol/reset. No physical-heap,
tail-latency, universal speedup or release-wide neutrality is established.

Original compile-to-result candidate processes differ20.48%; retain that uncertainty
and every raw point. Same-binary B715 reverses process/source construction order and
confirms the affected fresh direction with2.38%candidate spread. Text control stays
modestly adverse (initial+1.83..4.79%, confirmation+0.49..2.59%elapsed with CPU support).
Fulfillment's confirmation has4.41%candidate/3.22%reference spread and remains
inconclusive. **Retain with explicit small text debt and list allocation tradeoff.**
No unchanged further retiming is selected. A bounded cursor/enumerator follow-up may
address actual extra representation; no new indexing pool or call framework is selected.

The baseline already defers contexts and uses direct positions for list/tuple loops;
do not generalize the text-input allocation gain or attribute it to every former for
helper. Source eligibility tests establish both small and owned-wide native loops.
123internal/252public focused cases per actual11/10 pass, plus two post-promotion
output cases per target through four existing call drivers. Python controls cover
loop exits/else, nested loops, mutation, Unicode, exact integers and aliases. Real
I/O/native-child transfer, cancellation, terminal refusal, retained results and budget
reuse preserve ownership. Early promotion-fixture failures and corrections survive;
they establish mistaken fixture assumptions, not production defects. Assertions,
timeouts and stress scales are unchanged. Full delivery qualification is pending.

First B714 staging failed before a service/guest started because the immutable
reference link was missing. Complete the same verified partial root under a fresh
idle lease; no image/evidence retirement or VM-floor relaxation. B714 completes in
373.44s under420s ownership; B715 in226.09s under300s. All workers join, RAM fixtures
are removed and archives are independently verified under ownership. Terminal
states are cached. Publication and adapter promotion remain held.

[B714 timings](b714/summary.json), [same-bit confirmation](b715/summary.json),
[allocation](allocation-summary.json), [list allocation](allocation-list-summary.json),
[decision](decision.json), [raw B714](b714/observations.tgz),
[raw B715](b715/observations.tgz), [sources, binaries, failures and tools](source-and-checks.tar.xz).
