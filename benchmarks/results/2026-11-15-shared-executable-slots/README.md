# Shared executable storage and compiled returns

2026-11-15, R373. Retain the completed managed-slot migration, with explicit
regression tradeoffs. Full platform/stress/installed-consumer qualification is
pending. Publication remains held; this is not completion of the performance goal.

Compare qualified `4beb9ecf` / frozen `bed6e57e` with first layout `5db7e4f2`
(B762) and completed return/completion transport `95798eb9` (B763). Exact 585-input
source manifests, source archives, frozen cores and complete auxiliary-image
hashes are preserved. The identical optional adapter remains at 0.4.

| Complete workload / phase | First layout elapsed change | Completed layout elapsed change | Completed layout wider CPU change |
| --- | ---: | ---: | ---: |
| Fulfillment, warm | +0.53..+1.83% | -1.17..+2.40% | -3.17..+3.67% |
| Fulfillment, compilation | +0.37..+2.05% | -1.38..+0.64% | -3.78..+1.05% |
| Fulfillment, compile-to-result | +2.20..+2.91% | +0.30..+2.76% | -1.67..+1.72% |
| Invoice, warm | -1.26..+1.21% | -2.80..-1.81% | -2.94..-1.49% |
| Captured text, warm | -3.13..+1.61% | -6.60..-1.30% | -5.06..-2.11% |
| Short helper diagnostic, warm | -3.69..+0.36% | -10.04..-5.45% | -8.76..-3.17% |
| Observed-value diagnostic, warm | +4.31..+5.34% | +2.83..+4.65% | +4.28..+5.04% |
| JSON aggregation, warm | -3.42..+1.00% | -5.15..+2.02% | -4.90..+0.70% |

Changes compare every candidate/reference process pairing; positive means slower.
Each collection has eight jobs, two fresh processes per source/job, rotating
five 0.5s batches after full-request warmup (5s, text 12s). B762 completes in
304.747s and B763 in 305.254s on the quiet Azure F4as_v7, Ubuntu 24.04.4,
four EPYC 9V45 vCPUs, 16 GiB/no swap, .NET 11 RC3, ordinary GC/tiering.

The completed invoice result has stable process spreads (reference 0.71%,
candidate 0.30%) and CPU support: a modest demonstrated improvement. Fulfillment
fresh remains adverse; the observed-value diagnostic is reliably slower.
These are accepted 0.11 tradeoffs for the removed storage machinery and invoice
benefit, not hidden as noise. Text reference spread is 3.88%, short 4.39%, and
JSON 6.36%; their precise causal magnitudes remain uncertain. The original adverse
results remain authoritative observations of the first layout.

Separate desktop allocation runs use the unchanged worker, whole scripts,
goldens and shared-budget cleanup checks. Completed allocation changes are
invoice -7.89%, fulfillment -0.85%, short diagnostic -18.83%, text +0.05%.
The return refinement makes essentially no additional CSV allocation saving:
final values still need stable objects when exposed through aliases or containers.
The direct-consumption diagnostic illustrates a different boundary; it does not
predict complete-script gains. Allocation establishes neither CPU nor heap/tail
latency improvements.

One managed reference-plus-integer layout now serves final function arguments,
locals, operands and compiled return/completion storage. Remove the separate
native long arrays, loans, flags, capacity facts and generic cache parameter,
plus the unused builtin storage owner. Builtins/callbacks retain real object ABIs;
receiver methods borrow existing array windows without conversion arrays.
Finally observation materializes the original returning alias. Wider reference
storage can reject tighter allowances earlier. No public API, unsafe code,
new pool, option, controller or memory-management subsystem is introduced.

Both frameworks pass 319 focused internal and 6,996 selected public cases;
140 isolated CPython comparisons and 16 engine/workload phase goldens match.
Cancellation, terminal refusal, active/idle/escaping funding and joined cleanup
remain checked. Obsolete bank-only tests disappear; distinct refusal/cancellation
assertions move to function-workspace coverage. Initial fixture/compiler failures,
matrix-assignment routing correction and probe SDK-environment failures survive.

Warm time includes mediated RAM-fixture I/O, full stdout/file validation and
prior-result cleanup; it excludes compilation, startup, reset/protocol and final
cache shutdown. Compile-to-result adds compilation/script disposal. Wider CPU
includes reset/protocol. This source pair contains no CPython timing comparison.

The first VM owner failed before collecting data because its shell launcher had
CRLF line endings. The original launcher, failure and explicit LF-only correction
are preserved. Both successful collections archive and verify under the lease,
join every worker and remove RAM fixtures. No VM user remains.

Raw evidence and summaries: [first layout](phase-one/timing-summary.json),
[completed layout](completed-layout/timing-summary.json), and
[retention/tradeoff decision](decision.json). Source/check archives also retain
the originals, both core images and failed conditions.
