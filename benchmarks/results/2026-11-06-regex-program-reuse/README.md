# Reuse validated regex programs — R23

Retain immutable provider programs already compiled for frontend constant analysis
under the compiled script's existing ownership. Exact UTF-8 requests/normalized
options borrow them at runtime, removing duplicate provider compilation even for
a newly generated script run once. Runtime pattern wrappers, 512-entry caches,
purge identity and guest-visible group-name strings remain per execution. Dynamic
requests compile normally. No global cache, new allowance, refcount, tracing,
producer pin change or core backend dependency is introduced.

The old analysis-only owner/handle bookkeeping moves to the compiled artifact;
there is one program owner, not an additional reclamation mechanism. Invalid
compilation and standalone/import analysis still retire their handles immediately.
Handles close before funding; all runs must finish before script disposal.

## Tradeoffs and ownership correction

Normal0.4 public-boundary probe: baseline compiles/disposes once during Compile
and once per Run (3/3 total for two runs). Candidate retains one compilation
through both runs, closes once on script disposal and returns shared funding0.
Compiled baseline39,397→105,481B: programs intentionally remain resident longer
and earlier admission denial is possible. No heap/RSS/embedding-latency claim.

The initial design proposed sharing analyzed group-name PyStrings. Source review
found their lazy caches/derived values could use the compile governor concurrently.
Before timing, remove that sharing: only immutable provider programs/metadata
borrows cross runs; each run still admits/copies guest-visible names. No freezing,
new lock or ownership layer is added to rescue it.

Hash cold native keys with the current compile/run token using existing bounded
chunks. Cached programs retain no prior token. Exact requested options are keyed;
an analysis effective-flag alias is not blindly promoted across a distinct verbose
protocol request. New programs bind the existing provider consistently across runs
and imports; import-local artifact reuse is outside this bounded change.

## Controlled comparison

2026-11-06 UTC, quiet F4as_v7/Ubuntu24.04.4/.NET11RC3. B369 uses normal0.4,
unchanged workerb94fa98c/host and original extraction fixture/goldens. Reference
B368 corea87cd017 has all baseline41c25958 production source; candidate23a0a257
is41c25958 plus the archived patch. Ready messages verify actual loaded images.
Four jobs, three roles (reference/current/identical current), 5s warmups/role,
seven rotated150ms rounds, normal GC/tiering. Completes88.55s under150sowner/
30sstop; no SSH before180s. Quiet endpoints, all output/file goldens, terminal
balances, Closed/joins, fixture cleanup and under-lease verified archive pass.

| Boundary | Reference | Current / identical current | Elapsed change |
| --- | ---: | ---: | ---: |
| Regex extraction / warm |2.7925ms|2.5597 /2.5232ms|−8.34 /−9.64%|
| Regex extraction / compile-to-result |3.0313ms|2.8332 /2.8353ms|−6.53 /−6.47%|
| Text / warm control |2.2504ms|2.2282 /2.1662ms|−0.99 /−3.74%|
| JSON / compilation control |64.833us|63.755 /64.717us|−1.66 /−0.18%|

Extraction role spreads1.43%warm/0.07%fresh; IQRs below0.64%. Text2.78%role
spread and moving unchanged compilation limit precision. Process CPU ticks are
coarse and separate. Retain for demonstrated duplicate-work removal and bounded
elapsed evidence with explicit resident-memory tradeoff; no exact causal CPU
fraction, closure of text debt or refreshed CPython ratio follows. Warm includes
mediated RAM I/O, full checks and prior-result cleanup; fresh adds compilation/
program disposal. Startup/staging/final teardown are excluded. Do not subtract
independent medians to infer compilation or cleanup cost.

## Qualification and original conditions

Focused95internal/497public per actual.NET11/10 pass, plus the targeted cached
program cancellation/held-cleanup case on both. Release build0warnings/errors.
Cooperating provider checks current preparation/matching tokens after compile
token cancellation, terminal wrapper refusal, suspended cleanup/funding, replay,
cache purge and final0. Real0.4 adapter checks eight concurrent runs with Unicode
names/astral captures and retained group dictionaries after script disposal.
Complete local ordinary4274internal/16205public pass per actual.NET11/10;
explicit Release installed-package consumers pass both. Full Windows/Ubuntu/
original Stress delivery remains separate and pending.

Initial six public failures enforce the former successful-analysis disposal/token
observation assumptions; preserve their semantic/funding assertions and observe
the current token at actual preparation. A new concurrent test first hit xUnit1031
for Result; replace with awaited disposal, no analyzer suppression. All old logs
remain. A private diagnostic initially loaded a frozen baseline DLL through
MSBuild candidate search: its recorded f6fe357 hash proves it is not a candidate
measurement. Enforce a pre-run image gate, correct search to explicit HintPath
and retain both original output and refused verification. No VM timing used it.
The installed consumer first fails its old analysis-time disposal assertion.
Update it to require retained programs, unchanged acquisition counts across both
APIs, per-run funding recovery and final handle closure/root0 on script disposal.
Preserve its original failed log and both corrected actual-runtime successes.

Near-full VM: retire only the exact8,275,272B redundant CSV perf copy after hash
equality with the independently committed/pushed archive93074207, ended users,
exclusive lease and dependency checks. Keep all sources/maps/binaries/raw archives.
Publication remains postponed. Whole-script performance objective remains open.
