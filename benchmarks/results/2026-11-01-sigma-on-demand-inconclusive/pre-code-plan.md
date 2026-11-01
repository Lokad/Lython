# Lokad.Lython 0.11.0 — active plan

Updated **2026-11-01** (Paris). Two independent tracks. Missing producer releases
defer only final adapter qualification; core development, contracts, correctness
and non-regex performance work continue independently. No publication/tag authorized.

## Lasting constraints

Keep fully managed/memory-safe C#, managed stack admission, host capabilities,
cooperative CancellationToken control, terminal admission refusal, major-capacity
and replacement preflights, retained aliases and awaited cleanup. No unsafe/
Unsafe/pointers/PInvoke/native probes, quotas/fuel/watchdogs, hidden allowances,
heap tracing, forced GC or per-object ledgers. Use existing borrowed-budget owners;
scope creation reserves nothing. AGENTS.md and docs/RUNTIME.md are authoritative.

## Delivery — cache terminal evidence

- B27 runtime **830c6251**, original-scale fixture correction **3fedf3f6**: full
  checkpoint **41266997253** at 7d0157e1 is **terminal green** on Windows/Ubuntu:
  both ordinary frameworks, original stress and installed consumers. Never
  requery it. B27 and the unchanged 2,000-construction fixture are qualified.
- B32 **ae22392d** is committed/pushed. Eight regressions fail before the fix;
  focused Release **36 native / 169 public per actual framework** pass afterward,
  including suspended __index__ I/O, interrupted word/whitespace scans, spans,
  no further substantial payload admission and zero cleanup/unwound frames.
- Full B32 Windows/Ubuntu, both-framework, original-stress and installed-package
  checkpoint **41269118889** is terminal green on both platforms: ordinary both
  frameworks, original stress and installed consumers. Cache it; never requery.
  B27/B32 delivery is qualified, without another unchanged full cycle.
- **213a3134** ordinary push checkpoint **41271252227** is terminal green on
  Windows/Ubuntu, qualifying the retained B33 boundary tests. Never requery.
- Explicit Release pack and isolated consumers at **ae22392d** pass on actual
  .NET 11 RC3 / 10.0.8; empty runtime NuGet graph, symbols/guides and mediated file
  behavior pass. Canonical unpublished packages are only artifacts/nuget/.
  Adapter stays non-packable. Published 0.10 packages/tag remain unchanged.
- **41264726223** is terminal failure: ordinary both-framework/package checks
  pass on both platforms; only original net11 stress JSON subclass construction
  exceeds its former 4 MiB allowance. Preserve all 2,000 calls; corrected fixture
  uses 6 MiB, adds a peak bound, and all 14 family cases pass per framework.
  Do not requery failed/cancelled runs or older terminal green evidence. The
  B27/B32 receipts are recorded in tracked history; no unchanged full cycle
  or package rebuild is needed for these evidence-only updates.

The current CPython guide measures **830c6251**, not later HEAD. Warm diagnostic
gaps are 2.08x CSV, 4.93x text, 1.84x JSON; fresh text is inconclusive. CSV
compilation is 94.64 us vs Python 54.71 us / earlier Lython 330.30 us. Warm text's
old/new +8.06% observation remains visible. Never subtract independent phase
medians, attribute aggregate changes to one mechanism or treat allocation as
proof of latency. No repeat of closed comparisons/captures for favorable results.

## Track A — complete architecture; final released-adapter gate

Delivered core owns Python-specific native UTF-8 names, byte ranges and scalar
positions, original-subject windows, Lython split/sub/expand, ownership and current
tokens across frontend validation/constant capture analysis, runtime and imports/
fnmatch/glob. One borrowed provider; no backend types/dependencies/dialect fallback.
Contract tests, Python semantics and real backend behavior/performance are distinct.

Ordinary optional adapter remains **PythonRe 0.4.0**. A04/A05 native cursor/funding
implementations pass against exact private **0.5.0-dev.1**, producer source
0e24cecebbb9093a18f5cb962dae3b6f6426742c, PythonRe package SHA256
`e4136eddffdca5da0b9e5733cd9534053d7d99880714669b3695be5305ed7019`.
Evidence/patches preserve development plumbing outside core; standard restore/CI
never depends on private packages. No sibling source was modified.

Final capabilities still unqualified against a released immutable package:

| Capability | Final acceptance |
| --- | --- |
| Native names | Replace legacy GetGroupNames encoding if native enumeration is supplied; preserve effective/reported Python flags and metadata. |
| One-shot ranges | Direct match/search/fullmatch ranges and working funding receipt; inspected XML lacks those overloads, so conservative detailed snapshots remain. |
| Reversed window | Qualify pos > endpos zero-width matching with original prefix/anchors/lookbehind; core/test provider supports it, current adapter rejects explicitly. |
| Programs/cursors | Qualify funded compile/cursors, current-token moves, independent borrows and disposal after all uses against final package. Actual in-flight interruption, not entry/exit checks. |

Run dependency inspection, existing semantics/lifetimes/refusal/reuse controls,
isolated real cancellation probes, producer safety assessment and one focused
backend comparison only against a new immutable released pin. Core has no runtime
NuGet dependencies. Do not rerun unchanged development gates or poll the package
index. One subsequent local artifact inventory still finds only the same dev.1,
not a new pin; no qualification is repeated. Only this final gate waits.

## Track B — next coherent performance decision

B32 closes the demonstrated cancellation gap in all seven forward/reverse
whitespace rune loops using existing cooperative checks and explicit context
forwarding. Python whitespace/maxsplit semantics and ownership stay unchanged.
No speedup claim. Individual CLR bulk calls remain non-preemptible; cancellation
is cooperative, without a hard deadline. Do not expand into a text-search framework.

B33 is closed/rejected. [One fixed six-process comparison](benchmarks/results/2026-11-01-bulk-whitespace-rejected/README.md),
47.122 s, valid quiet/golden/ownership gates. Complete text CPU -6.84% against
first baseline / -4.78% against final identical control; allocation -2.375%
against either. Misses predeclared >=5% for BOTH metrics against BOTH baselines.
Restore both runtime files byte-for-byte: done. Release solution build passes;
three independent CPython UTF-8/control/maxsplit boundary cases pass the restored
runtime per actual framework and remain. Prototype focused 36 native / 172 public
per framework passes. No scanner, phase layer, public API, tuning switch or
allocation-policy change remains. No retiming or smaller rescue optimization.

B34 is closed. [Two current-source allocation captures](benchmarks/results/2026-11-01-current-allocation-origins/README.md),
128 complete jobs each / 2.065 s total, pass quiet/golden/joined-cleanup gates.
Actual named guest threads, complete allocation categories/origins, all-thread
CPU and raw traces are preserved. Approximate frame/context bytes: 26.13% text /
18.85% CSV. BigInteger boxes and temporary reservations remain distributed costs;
object arrays also contain required result storage. These are not CPU fractions.
Background JIT CPU remains visible; no steady-state or latency claim.

The bounded source/lifetime decision selects no prototype: namespaces are already
lazy, arguments already become local storage, and lexical/suspended caller state
remains live. Combining objects does not delete those fields. An indexed frame
arena needs a concrete stable-identity, suspension and awaited-cleanup design;
these samples alone do not justify it. No pool/reset rules, context fusion,
universal tagged values, JIT, new policy or marginal reservation tweak is added.
Stop this diagnostic, not independent runtime development. Preserve its criteria
in .git/agent-notes/PLAN-history-through-B34-criteria.md; no recapture.

B35 is closed/rejected. [Shared iteration-cursor prototype](benchmarks/results/2026-11-01-iteration-cursor-rejected/README.md),
six processes / 47.847 s, removes CheckedEnumerator and sync wrapper detours but
misses fixed targets: text CPU +2.58% / -2.80%, allocation -3.56% against both.
Identical text CPU drifts +5.537%, also failing the fixed 5% validity gate. Quiet,
goldens, funding and joined-cleanup gates pass; preserve all adverse observations.
Restore all three runtime files byte-for-byte: done. No cursor fields, indexed
paths, routing changes or rescue tweak remains. Candidate focused Release 172
native / 427 public per actual framework passes. Restored build and the new
independent mutation/exhaustion/generator regression pass per framework; retain
it and remove only an old exact cancellation pull-count assertion, preserving
bounded interruption/disposal checks. No unchanged full cycle/repack. Stop B35.

B36 — final-sigma context on demand. Current-source B29 attributes 10.7% of text
guest self samples to SigmaPropertyContains. Source still maintains preceding-
cased state on every rune for lower/capitalize/swapcase/title, although only
uppercase Sigma consumes that state. Different algorithm from rejected B06
bulk-ASCII and B30 property-table replacement: keep both data and lookup intact.

Hypothesis/deletions: remove four per-rune context variables/updates; evaluate
Unicode's preceding/following original-subject context only at an actual Sigma.
Keep title's separate word-boundary state. Existing UTF-8 reverse-boundary helper
and property predicates suffice; no database, cache, scan framework or allocation.
Linear-work argument: Sigma is cased and non-ignorable, so a scan stops at another
Sigma; each intervening ignorable run is traversed at most once per direction.
No repeated whole-prefix scans or index/cache build. Keep checks inside each scan.

Acceptance: independent CPython lower/capitalize/swapcase/title goldens with
cased-and-ignorable overlap, supplementary runes, empty/punctuation boundaries,
expansions and long ignorable runs between adjacent Sigmas. Existing exact casing,
large input/Unicode, output growth/terminal refusal, cancellation and retained
owner cleanup cases pass in both APIs/actual frameworks. No byte ownership,
builder/admission/rollback or public behavior change. No new cancellation gaps.

One fixed complete-text + CSV control comparison: before/candidate/identical-before,
six processes, same maintained catalog/driver, 4 warmups then 7 x 128 jobs each;
<=30 s/process and <=90 s aggregate, quiet VM lease/gates and full goldens/cleanup,
ordinary GC/tiering. Accept >=5% lower text CPU against BOTH baselines; allocation
<=2% worse, CSV <=5% CPU/2% allocation worse. Identical-before CPU drift <=5%,
allocation drift <=0.2%; every CPU batch >100ms. Preserve wall/CPU/allocation rows.
Reject and restore on target or validity failure. No retiming, table/ASCII rescue
or changing these thresholds after results. Focused Release first; broad platform/
package qualification only at a coherent retained-code delivery boundary.

Remaining performance deliverable: reduce a demonstrated dominant cost in
representative complete scripts, including configuration editing, file search,
CSV/JSON aggregation and report generation. Reuse the existing phase, CPU,
allocation and ordered-control evidence before commissioning another capture.
The CPython gap remains open. Neither rejected prototypes nor lower allocation
alone count as a performance result. A future candidate must first name the
state/conversion/transport that disappears and its whole-workload importance.

Before new code, record its hypothesis, affected workloads, expected deletions,
compatibility/lifetime tradeoffs and Python semantic acceptance. Cover sync/async,
closures/generators where touched, in-flight cancellation, refusal before mutation,
retained aliases and budget reuse. Freeze one short before/candidate/identical-
before comparison, material improvement threshold, control-drift bounds and
cleanup/verification gates. Stop and remove the prototype on failed acceptance;
no tuning/retiming for a favorable result. This acceptance requirement does not
block correctness fixes or the independent regex contract track.

## Completed work and history

Full completed milestones, original acceptance criteria, terminal CI IDs and
experimental outcomes are preserved in [the ledger through B32](.git/agent-notes/PLAN-history-through-B32.md).
Do not repeat their unchanged qualification. Recent outcomes:

| Item | Outcome |
| --- | --- |
| B25/B26 | Direct Python lexer removes Parsing/core's last runtime dependency; compilation allocation -22.7–24.2%. Python indentation/EOF diagnostics corrected. Full checkpoint 41262561230 green. |
| B27 | Capacity ownership deletes dictionary entry ledger/reserve/refund paths; namespace/attribute owners preserved. CSV allocation -0.342%, accounting +832 B. [Evidence](benchmarks/results/2026-11-01-capacity-admission/README.md). |
| B28 | Seven complete phase jobs / 58.22 s, CPython guide refreshed; uncertainty/regressions retained. [Evidence](benchmarks/results/2026-11-01-representative-checkpoint/README.md). |
| B29 | Fixed current CPU capture / 15.676 s; sigma-property searches 10.7% text guest self samples. [Evidence](benchmarks/results/2026-11-01-current-text-cpu/README.md). |
| B30 | Property-index prototype rejected: -6.85% / -4.15% CPU misses fixed -5% against both baselines. All runtime code restored, no retiming. [Evidence](benchmarks/results/2026-11-01-sigma-properties-rejected/README.md). |
| B35 | [Shared cursor rejected](benchmarks/results/2026-11-01-iteration-cursor-rejected/README.md): six processes / 47.85 s; target and control-drift gates fail. Runtime restored, independent semantic coverage retained. |
| B34 | [Current allocation origins](benchmarks/results/2026-11-01-current-allocation-origins/README.md): two captures / 2.065 s, frame/context traffic material, no justified deletion prototype after bounded lifetime review. No runtime change or latency claim. |
| B33 | [Bulk whitespace prototype rejected](benchmarks/results/2026-11-01-bulk-whitespace-rejected/README.md): six processes / 47.12 s, CPU/allocation miss fixed targets. Runtime restored, three independent boundary regressions retained. Stop prototype. |
| B31/B32 | Bounded text-construction read identifies real scan cancellation gap; ae22392d fixes it. Focused both-framework and installed Release consumers pass; full checkpoint 41269118889 green. |

Current docs stay concise and about supported behavior. Plans, transient receipts,
rejected approaches and investigations remain outside docs/. Immutable benchmark
evidence belongs under benchmarks/results/. Keep original stress scales/timeouts;
focused Release work during development, broad qualification at coherent boundaries.
