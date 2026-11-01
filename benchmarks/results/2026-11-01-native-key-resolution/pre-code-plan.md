# Lokad.Lython 0.11.0 — active plan

Updated **2026-11-01** (Paris). Two independent development tracks. An unpublished
producer API gates only final optional-adapter qualification, never core work.
No publication or tag is authorized. PLAN.md remains intentionally untracked.

## Lasting constraints

Fully managed and memory-safe C#; managed stack admission, host mediation,
cooperative CancellationToken control, terminal admission refusal, major-capacity
and replacement preflights, retained aliases and awaited cleanup. No unsafe /
Unsafe / pointers / PInvoke / native probes, quotas / fuel / watchdogs, hidden
allowances, heap tracing, forced GC or per-object ledgers. Borrow existing host
budget scopes; scope creation reserves nothing. AGENTS.md and docs/RUNTIME.md
specify the current contracts. No guest work resumes after refused admission.

## Current delivery — B38 mixed-key correctness and simpler copies

**Demonstrated problem:** the verified ae22392d runtime at 8d099a1f keeps two
entries after a native key updates an equal custom key. First 34 independent
CPython 3.13.2 cases expose 25 mismatches. This correctness work is separate from
B37's rejected probe-count optimization; no timing against the incorrect cases.

Completed implementation and focused qualification:
- Reuse the existing reentrant protocol-resolution loop for native misses with
  custom keys, and mixed-key setdefault. Keep original key identity/order,
  callback effects/errors and checked admission. Extract the unchanged native
  write body; unmatched native entries acquire no protocol side bookkeeping.
- Restore ambient provenance around staged |= application and Counter merges.
- Native defaultdict construction uses the existing funded copy constructor,
  preserving stored hashes; general sources share UpdateDictionaryFromSource.
  Delete both duplicate population routines and their tuple-only pair checks.
- Copy storage directly at known capacity, admitting unknown-source growth.
  Delete the entry ToArray and obsolete copy scratch reservation. Preserve
  rollback, cooperative checks and retained source/destination ownership.
- 45 CPython semantic goldens and 4 cancellation/refusal cases pass both APIs;
  broad focused Release: 355 native / 880 public per actual target. Two new
  native copy-admission/cancellation tests pass each target. Shared-budget
  baseline/reuse and terminal-refusal origin checks remain active.
- One short correct native-copy control matches CPython before/after. Governed
  peak estimate 197,885 -> 164,669 bytes; this is accounting, not CLR heap use
  or a speedup measurement. Preserve raw controls outside current docs.

Delivery status:
- Async constructor ambient scope is now confined to synchronous work. Two
  genuine-suspension cases fail before correction and pass afterward. Final
  nonincremental build: zero warnings/errors; 14 native / 51 public focused
  cases pass per actual target. Full local ordinary suites before that final
  scope correction pass 3,954 native / 15,382 public cases per target.
- Current runtime docs/changelog and immutable evidence are complete:
  [B38 evidence](benchmarks/results/2026-11-01-mixed-dictionary-keys/README.md).
  Committed/pushed **1c369000**, following B37 evidence **c57f9752**.
  Evidence/accuracy follow-ups through **3fe29ebf** change no src/tests/tools/CI.
- One full manual checkpoint **41279594583** is running for exact 1c369000:
  Windows/Ubuntu, both-target ordinary/stress and installed Release consumers.
  Latest observation: BOTH ordinary frameworks and installed consumers pass on
  BOTH platforms; original stress is still running. Push-only duplication is
  skipped deliberately. Inspect minutes apart; cache its terminal result, never
  requery older terminal runs. No new full cycle for evidence/docs-only HEAD.
- Local explicit Release pack/isolated consumers PASS on actual .NET 11 RC3 and
  10.0.8 at 1c369000, empty runtime dependency graph. Only core packages in
  artifacts/nuget; published 0.10 output hashes and tag remain unchanged.

Compatibility: correct custom equality callbacks can now run where previously
bypassed. Hash/key-equality synchronous-completion restrictions remain explicit;
this change does not introduce asynchronous key-protocol infrastructure. Native
copy estimates may reject at different thresholds; live funding/rollback remain.

## B40 — high priority mixed-key native-hit callback ordering

**Demonstrated after B38:** custom key inserted first with the same hash as 'x'
but false equality; insert native 'x', then arm equality to raise ValueError.
Four isolated CPython controls disagree on getitem/get/assignment/setdefault
(evidence/compatibility notice committed/pushed **870ac5b7**, clarified **3fe29ebf**):
Lython bypasses equality; CPython calls it and raises. Assignment mutates the
native value in Lython despite the callback failure that should precede it.
[Raw evidence](benchmarks/results/2026-11-01-native-hit-protocol-order/README.md).
B38 fixes native MISSES; exact-hit ordering remains unfixed and documented.

Next coherent design: shared ordered candidate resolution across lookup/write/
setdefault using existing storage/hashes, preserving native-only fast paths and
reentrant key replacement/removal. Do not simply route all hits protocol-first:
that would call later custom keys before an earlier native match. Compare native
first/custom first and same-hash unequal/equal/error cases before changing code.
Identify which duplicate resolution paths disappear and bound any temporary
snapshot at admission. No per-native-entry identity ledger, new tracing/index
framework, cache/policy switch or broad dictionary rewrite by default.
If ordered resolution needs substantial new machinery, state the tradeoff and
propose a conservative funded alternative first; do not silently weaken behavior.

Acceptance: both insertion orders, callback errors/effects and mutation/restart,
key identity/order, update/union/derived mappings, both APIs, cancellation and
terminal refusal before effects, retained aliases and budget reuse. Focused
Release initially; next full delivery only at a retained-code boundary. This is
correctness, not another tiny native lookup optimization or a timing comparison
against a semantically wrong baseline. Implementation selected before code: one private native-key resolver using existing
funded protocol snapshots and ordered-store IndexOf. Carry context explicitly and
await supported lookup equality. Writes retain synchronous callback restrictions.
Revalidate candidate sequence identity after callbacks, including same-object
delete/reinsert; compare current native/candidate ranks before dispatch. Native-only
operations keep their direct path. Apply the same resolution to membership/removal
so the fix does not leave equivalent bypasses. Popitem/native cleanup remove their
already-resolved stored key without callbacks. Expected cleanup: remove the duplicated early
native-hit decisions from the affected lookup/write/setdefault paths as ordered
resolution is shared. Keep native-only dispatch and the useful existing side
index, allocation preflights and snapshot rollback; no new general dispatcher.
Additional equality effects/errors are intentional Python corrections. Coarse
snapshot funding can reject earlier; retained owners and pending async users must
remain charged. No timing result is required to justify the demonstrated fix.

Bounded source assessment completed: both TryGetValue overloads, string-specific
async lookup and write/setdefault paths short-circuit exact native hits. The
string async path genuinely awaits ElementEqualsAsync; preserve that supported
behavior (write/hash restrictions must not be generalized to all lookup equality).
Existing protocol snapshots already carry frozen hashes/sequence and are funded.
A candidate correction can compare existing protocol ranks with the current
native-hit rank using the ordered storage, revalidating after callbacks; do not
add a per-native-entry index/ledger. Simply filtering one initial snapshot is
insufficient when callbacks remove/reinsert the native key. Preserve explicit
context through awaited equality, never thread-static scopes across suspension.
If that cannot share a small invariant across callers, consider one funded ordered
mixed-key snapshot with an explicit O(n)/earlier-refusal tradeoff; native-only
lookups must retain their path. Acceptance frozen before code: independent Python cases for both insertion orders,
all native-key operations, callback false/true/error, deletion/reinsertion and
mutation. Include genuine pending equality through string lookup, cancellation,
terminal refusal before callbacks and budget cleanup/reuse. One short native-only
correct complete-script before/after/identical-before diagnostic (no gain claim or
retiming); no timing against semantically wrong mixed-key baseline. Use the existing
text script and fixed 4 warmups/7 batches of128 jobs, <=30s per child /90s total,
ordinary tiering, source/hash/golden/quiet/cleanup controls. Native-only CPU drift
<=5% / allocation<=0.2% establishes control validity, candidate regression<=5% CPU /
2% allocation is a diagnostic guard; correctness is not gated on a speedup.

## Track A — complete Python-specific regex architecture

**Delivered:** Lython-owned native UTF-8 metadata, byte ranges/scalar positions,
original-subject windows, shared split/sub/expand, ownership/current tokens across
frontend validation/constant analysis, runtime, imports, fnmatch/glob. One borrowed
provider. No backend types/dependencies/dialect fallback in core. Contract tests,
Python semantic goldens and actual backend performance are separate evidence.

Ordinary optional adapter pin remains PythonRe 0.4.0. A04/A05 cursor/funding
implementation qualified exact private 0.5.0-dev.1 only, producer source
0e24cecebbb9093a18f5cb962dae3b6f6426742c, PythonRe package SHA256
`e4136eddffdca5da0b9e5733cd9534053d7d99880714669b3695be5305ed7019`.
Development bridges stay outside core and never affect ordinary restore/CI.
No sibling source is modified; do not requalify unchanged private artifacts.

**Final adapter gate only:** wait for a new immutable released package, then:

| Missing/unqualified capability | Acceptance and temporary development boundary |
| --- | --- |
| Native names | Replace adapter-only GetGroupNames encoding when supplied; retain effective/reported flags and capture metadata. |
| One-shot ranges | Direct match/search/fullmatch ranges plus working-funding receipt. Current inspected API lacks overloads; conservative detailed snapshots stay in the adapter. |
| Reversed windows | Qualify pos > endpos zero-width matches with original anchors/prefix/lookbehind. Core/test provider supports it; current real adapter rejects explicitly. |
| Programs/cursors | Requalify funded compilation/cursors, current-token moves, independent borrows and disposal after users finish. Demonstrate actual in-flight interruption. |

Use existing dependency/semantic/lifetime/refusal/reuse controls, isolated real
cancellation probes, producer managed-safety assessment and one focused backend
comparison against the new pin. Temporary adapter/test-provider conversions are
acceptable for development, never core plumbing or a backend performance claim.
No package-index polling; missing producer capabilities stop only this final gate.

## Track B — substantial non-regex performance redesign

The current CPython guide measures **830c6251**, not HEAD: diagnostic warm gaps
2.08x CSV / 4.93x text / 1.84x JSON; fresh text inconclusive. CSV compilation
94.64 us vs Python 54.71 us / earlier Lython 330.30 us. Warm text's adverse
+8.06% historical old/new observation remains visible. These are diagnostics;
phase medians cannot be subtracted, nor allocation called a latency improvement.

**B39 closed — bounded JSON/configuration attribution:** one fixed pair of
512-job captures on verified 1c369000, 6.093s total / 17.792s collection; quiet,
complete-golden and joined-cleanup gates pass. JSON: 713 guest samples, main loop
108 / Monitor enter-exit101 / object-reader MoveNext17; these are self samples,
not removable or whole-parser fractions. Configuration:54 guest samples, too
sparse for dominant-cost attribution. All-thread JITCPU0.69/0.72s remains visible.
Source already uses native UTF-8 tokens and synchronous scalar paths; no justified
parser duplication, universal frame/value rewrite or synchronization deletion.
Stop B39, no recapture. Evidence committed/pushed **eff33bee**. [Evidence](benchmarks/results/2026-11-01-current-json-cpu/README.md).
The substantial performance deliverable below remains OPEN; this diagnostic is
not an improvement or a reason to force another marginal patch.

Next deliverable: a coherent redesign removing demonstrated dominant transport,
state construction or conversions in complete agent scripts, rather than a new
series of local probe/scan/reservation tweaks. Cover configuration editing, file
search, CSV/JSON aggregation and report generation. Reuse B28 phase boundaries,
B29 CPU origins and B34 allocation/lifetime evidence. Current data shows main
instruction dispatch, distributed synchronization and native text work; frame
allocations alone do not establish a removable CPU fraction.

Before new code, record:
- **Hypothesis and deleted work:** name the transport/state/conversion that goes
  away, its measured importance, and affected complete scripts. Inspect call /
  argument / frame costs, boxed dispatch, text boundaries and frontend costs as
  alternatives; do not select a universal value/frame rewrite from object counts.
- **Semantics/lifetimes:** Python values/effects/order, sync/async calls,
  closures/generators where affected, real suspension, in-flight cancellation,
  admission before mutation, retained aliases and cleanup/budget reuse.
- **Bounded comparison:** one fresh-source before/candidate/identical-before
  collection, same inputs/outputs/runtime/tiering; distinct compilation, warm
  execution, I/O/cleanup boundaries. Freeze material-improvement and control-drift
  bounds before implementation. Include an unaffected script. No favorable
  retiming, phase subtraction, allocation-only speedup or invalid baseline.
- **Stopping criterion:** one bounded source/design decision using existing
  traces. Add a narrowly scoped profile only if a named missing attribution
  prevents that decision; freeze its duration and stop once answered. If no
  coherent deletion is justified, record why; do not force another small patch.

Fresh nonincremental builds are mandatory after timestamp-preserving restores.
Verify source markers and equal core/worker/test/probe copies before freezing;
a binary hash alone does not prove the declared source. Stop/remove a failed
prototype. Failed comparisons do not block independent correctness or Track A.

## Completed milestones and cached delivery

Full history/criteria are preserved in .git/agent-notes/PLAN-history-through-B38-design.md
and prior ledgers; immutable experiment evidence is linked below. Do not rerun
unchanged terminal checks or old comparisons.

| Milestone | Result |
| --- | --- |
| B25/B26 | Direct Python lexer, correct tabs/EOF diagnostics, last core runtime dependency removed. Compilation allocation -22.7–24.2%; full checkpoint 41262561230 green. |
| B27 | Capacity-level dictionary ownership deletes entry ledger. Runtime830c6251; original 2,000-call fixture preserved with corrected allowance3fedf3f6. Full41266997253 green Windows/Ubuntu. |
| B28/B29 | [Representative phases](benchmarks/results/2026-11-01-representative-checkpoint/README.md), [current CPU](benchmarks/results/2026-11-01-current-text-cpu/README.md); fixed collections complete. |
| B30/B33 | [Property](benchmarks/results/2026-11-01-sigma-properties-rejected/README.md) / [whitespace](benchmarks/results/2026-11-01-bulk-whitespace-rejected/README.md) prototypes rejected; runtime restored, independent regressions retained. |
| B31/B32 | ae22392d fixes real cancellation gaps in seven whitespace loops. Full41269118889 green Windows/Ubuntu, ordinary/stress both targets and installed consumers. |
| B34 | [Allocation/lifetime assessment](benchmarks/results/2026-11-01-current-allocation-origins/README.md); no justified frame pool/fusion/arena from these samples. No runtime change. |
| B35 | [Cursor prototype rejected](benchmarks/results/2026-11-01-iteration-cursor-rejected/README.md); independent semantics retained. Fresh-checkout41273799899 green. |
| B36 | [Sigma-context comparison invalid](benchmarks/results/2026-11-01-sigma-on-demand-inconclusive/README.md): stale restored binary. Prototype removed; nonincremental rebuild/copy identities and goldens verified.8d099a1f /41274744720 terminal green Windows/Ubuntu. No retiming. |
| B37 | [Write consolidation rejected](benchmarks/results/2026-11-01-dictionary-writes-rejected/README.md): misses CPU target, control drift. Runtime restored exactly, two semantic cases retained. Committed c57f9752. |

Older failed/cancelled CI and corrected restoration claims remain in history.
Canonical unpublished packages currently identify ae22392d; published 0.10
packages/tag are unchanged. Current docs describe supported behavior, not this
history. Focused Release development; full stress/platform/package checks only
at coherent retained-code boundaries. Original stress scales/timeouts stay intact.
