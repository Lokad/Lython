# Cell assignment authority and lexical correctness

2026-11-12, five production files over fully qualified `7114021e` production
(unchanged in198e5c40). Frozen candidate `09ad301e`, reference `0e715d19`,
with precommit metadata. Source inventories and final logs identify measured code.
Retained for delivery; full platform/stress/package qualification is separate.
Publication and tagging remain postponed. No public API or resource-policy change.

## Removed work, preserved lifetimes

One Value setter updates an active owner's known local slot and applies the
existing conditional namespace policy. The immutable slot is supplied at cell
creation; repeated name-to-slot lookup, StoreThroughOwner and duplicate local/
namespace writes are removed. Namespace-backed cells retain their current owner
policy. There is no new owner, tracing, scalar representation, registry or option.

An inactive frame may be returning a pooled workspace, not merely suspended.
The existing active-owner guard is preserved; inactive cells cannot write that
cache. Resume synchronization is necessary: plain generators refresh captured
cells, mirrored frames use the existing namespace sync helper. Long namespace
sync observes current cancellation in bounded chunks. No lifecycle flag, reverse
link, cleanup-order rewrite, fuel or forced GC is added. Existing ownership,
preflights, rollback and funding for retained aliases remain.

## Two demonstrated baseline defects

The first setter prototype updates inactive caches and removes resume refresh;
1,073 internal and1,524 public checks pass, but an existing comprehension case
fails. Original edit/originals, TRX/log and refinement record remain. Those first
checks do not qualify the final source. Source review retains the active guard
instead of adding pool-lifetime machinery.

An independent probe on the exact committed `7114021e` frozen core confirms hidden
corruption: after a comprehension shadows an enclosing nonlocal name, its outer
slot reads'outer' while a separate closure reads2. CPython keeps both'outer'.
Retained-cell synchronization now respects the existing inlined-binding barrier.
The old test remains and a separate-reader golden is added; deletion follows the
same lexical precedence.

A second baseline probe shows a suspended mirrored generator printingold after a
lowered class writes its nonlocal; CPython printsnew. Both baseline and initial
prototype fail. This is an existing namespace/cache discrepancy, independently
corrected with the existing namespace-sync helper at resume. Set and delete
regressions run through both APIs and check shared-budget cleanup/reuse. The
ordinary escaped-cell generator already matched9 before changes; no old bug is
claimed there. Isolated CPython3.13.2 verifies both new expected outputs; all three
corrected compatibility probes match.

## Focused checks and diagnostics

Final Release: **1,099 internal / 3,493 public checks per actual .NET11/10**,
9,184 total, on unchanged frozen source. Solution/probe build19.20s, zero warnings/
errors. Closure/nonlocal/lambda/class/annotation/generator/namespace/unbound cases,
real suspension/cancellation, reentry/refusal, retained funding, managed stack and
cleanup remain covered. No architecture-only test replaces a semantic assertion.

Three complete captured/ordinary-text/report scripts pass both APIs with complete
stdout/file hashes, allowance balances and fixture removal. Allocation is flat.
Existing common worker/adapter, shared CoreLib/GC,64warmups and five rotating rounds
of8jobs; desktop elapsed is diagnostic. Captured Run-4.06..+9.10%(12.07%control),
Async+2.86..+5.30%(2.31%control), reportRun-10.03..-8.98%(1.16%control) and all other
raw observations remain. No allocation-to-CPU inference or deletion of adversity.

## B606 targeted quiet comparison

Four jobs finish in**114.479s**. Existing quiet Azure F4as_v7 / Ubuntu24.04.4 /
.NET11RC3, fixed fec79afd worker and optional adapter0.4, ordinary GC/tiering.
Two independent processes per image, five-second warmups and seven rotating150ms
rounds. Negative means less time; four median pairings are not confidence intervals.

| Complete script / API | Elapsed change | Wider process CPU change |
| --- | ---: | ---: |
| Captured text / Run | -8.91..-3.17% | -8.41..-2.91% |
| Captured text / RunAsync | -6.96..-4.75% | -7.00..-1.26% |
| Ordinary text / Run | -0.02..+2.15% | +0.77..+1.54% |
| Report / Run | +0.55..+4.63% | -3.96..0% |

Captured improvement has supporting CPU. Accept the small **additional ordinary
Run CPU increase versus7114** at this delivery boundary. Report elapsed remains
uncertain with supporting CPU at or below zero; its controls1.07/2.85%, maxIQR3.45%
and all samples remain. This does not establish a causal recovery from adverse
desktop observations. Prior7114/82ed and82ed/2ec debts have different references;
do not compound ranges or multiply them into CPython ratios.

Warm clocks include public invocation, mediated RAM-file I/O, full output/file
validation and prior-result cleanup; exclude startup/compilation, reset/staging,
protocol and final cache disposal. Wider CPU includes protocol/reset. Desktop
includes fixture reset and immediate disposal. No updated CPython/RSS/embedding
latency or large-file throughput claim. Compilation unchanged, not remeasured.

All loaded-image/source/output/file/allowance/quiet-before-after/join/RAM/archive
gates pass; persistent archive independently SHA-verified, terminal cached. Initial
staging inventory named already-retired B599raw and failed before mutation; original
stderr/source/config conditions preserved. Final config replaces that entry with
exact committed B444raw. Superseded0106core aliases, B605raw/catalog and B444raw
were retired under fresh idle lease, UID/path/inode/all-hardlink and canonical Git/
SHA proofs. Current0e715 reference/candidate protected, OS/evidence floor unchanged.

Retain this common policy and the two independent correctness fixes. Complete one
coherent retained-source Windows/Ubuntu actual11/10, original stress and installed
Release-consumer check, then continue the wider roadmap. The whole-script objective
is not complete. No additional unchanged retiming or automatic scalar redesign.
[Decision](decision.json); current external rows remain in
[BENCHMARKS](../../../docs/BENCHMARKS.md).
