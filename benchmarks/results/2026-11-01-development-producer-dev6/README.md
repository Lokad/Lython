# Exact development producer and deferred error text (A06)

2026-11-01, Lython source `8588c4d7` plus the ordinary adapter correction in
[ordinary.patch](ordinary.patch.gz). Core production source remains `9003d045`.
**Development qualification, not promotion or final released-adapter qualification.**
The non-packable ordinary adapter still pins PythonRe 0.4.0. No backend dependency
enters core, no package is published, and no sibling source is changed.

## Exact development input and remaining capabilities

Both local 0.5.0-dev.6 packages identify producer commit
`825ed1f7c4b4eb54f0e092da495396d93b435a48`:

- PythonRe SHA256 `ffbfdcd07f85b44388b2d4352e36b0de1eafe641042c04c13c00c4a37ed928c4`.
- Base SHA256 `6ed5a8071923966e59315883d5e7cc870488c82f1422902b837cf6a5f32f30dc`.

Frozen private copies, isolated feed/output/intermediates/packages/locks and the
existing A05 funded adapter keep development independent of publication. The
[development source](Utf8RegexProvider.Development.cs.txt) and
[delta from the ordinary corrected adapter](development.patch.gz) carry producer
program/working allowances, range-cursor receipts, reused current capture storage
and the delivered scalar-error correction. Existing owners fund construction,
capacity/overlap and real users; no new allowance, tracing, retry, quota or cache.

[Immutable API source](producer-api-source.json) distinguishes UTF-8 name lookup
and pattern copying from the missing native name enumeration. One-shot
match/search/fullmatch still lack an independent working allowance/range receipt;
the conservative detailed-snapshot bridge remains. Reversed original-subject
match windows still fail explicitly. Current-token moves, effective/reported flags,
byte captures and scalar positions retain the existing Lython contract.
These gaps defer their final integration and released-package/safety qualification;
they do not stop independent core work. A source-policy search is not complete
binary safety certification. No public-release or final-safety claim is made here.

## Actual correction

The scalar-error bridge passed `backend.Pattern` to its helper eagerly. In this
exact UTF-8 producer, that property calls Encoding.UTF8.GetString on every access.
Thus each successful match/cursor move decoded the pattern solely for a possible
later error. Pass the backend handle instead; read Pattern only in the
pattern-error catch, after checking the current token. No deferred delegate,
extra copy/cache, alternative matcher or public API is introduced.

The ordinary 0.4 adapter receives the same compatible correction. It keeps its
failure-only UTF-16-to-scalar normalization and catchable Python errors. The
measured allocation change below concerns the development producer; do not claim
the same allocation reduction for 0.4, whose Pattern already stores a string.

## Focused validation

Both actual .NET 11 RC3 and .NET 10.0.8 pass **424 public and 84 native checks**
with the ordinary corrected adapter and with the corrected development adapter.
Public selections differ: the private build adds three funded-producer controls
and excludes the three default-path subprocess-discovery tests. The actual
isolated compile/match/cursor fixtures are run directly instead.

All six development probes observe work still running at cancellation, propagate
the original active token, join cleanup and finish at zero allowance. Ordinary
public checks include their normal owned subprocess probes. Existing tests cover
first-origin refusal/no retry, independent cursors, 4,096-advance balance reuse,
retained matches/caches, error metadata, captures, frontend flags and fnmatch/glob.
No semantic expectation, timeout, cancellation or ownership assertion is relaxed.

Five initial public failures are missing isolated workflow fixtures; an incomplete
staging correction leaves one Fnmatch fixture failure. Both runs are preserved.
Staging the current repository fixtures resolves them; no runtime fix is inferred
from these fixture-discovery failures. [All checks and inputs](summary.json),
[actual cancellation results](native06-cancellation.json), isolated imports and
logs/TRX in [focused-checks.tar.gz](focused-checks.tar.gz) preserve the evidence.

## Two separate bounded diagnostics

Reuse the existing allocation driver, three regex jobs plus a complete 1,000-line
text control. Windows 10.0.26200, 28 logical processors, .NET 11 RC3, ordinary
GC/tiering. Four warmups, seven rounds of five complete warm public RunAsync jobs;
full return/stdout goldens and result disposal included, compilation/startup
excluded. Each process has a fixed 30-second kill/join bound. All six processes
exit zero and are joined. No VM collection, retiming or latency qualification.

First compare producer dev.1 / dev.6 / identical dev.1 with the same eager-error
adapter and core/host bits. Dev.6 adds **72 bytes per regex job**; text is unchanged.
[All producer observations](allocation-summary.json) remain separate from the fix.

Then compare eager / deferred / identical eager with the **same dev.6 producer**;
only the optional adapter DLL differs. Frozen criteria require at least 5% lower
allocation for split/sub, identical-original and text-control drift at most 0.2%.

| Warm job | Eager B/job | Deferred B/job | Identical eager B/job | Change vs both originals |
| --- | ---: | ---: | ---: | ---: |
| Capture split, 300 matches | 408,910.4 | 387,166.4 | 408,910.4 | -5.32% |
| Template substitution | 244,128 | 222,384 | 244,128 | -8.91% |
| 100 match.expand calls | 203,896 | 203,824 | 203,896 | -0.035% |
| Complete text control | 228,168 | 228,168 | 228,168 | unchanged |

The allocation gate passes. Split/sub each remove 21,744 allocated bytes per job;
expand removes one 72-byte pattern string. Full elapsed samples, including adverse
rows and different original medians, stay in the JSON. They do **not** prove a
throughput gain or absence of latency regression. [Fixed comparison](bridge-summary.json),
all three full observations and [binary controls](bridge-identities.json) remain.
The maintained CPython table is unchanged. Final released-adapter qualification
still requires a new immutable released package with the missing capabilities.

Ordinary-adapter delivery is terminal green at `d703767c`: [run41343479748](https://github.com/Lokad/Lython/actions/runs/41343479748)
passes Windows/Ubuntu ordinary Release on both actual targets, original Stress on
both targets and explicit Release installed-package consumers. [Cached terminal
proof](delivery-ci.json) is authoritative; do not query that completed run again.
This qualifies the unchanged 0.4 dependency pin plus the consumer correction;
it does not promote or cross-platform qualify the unreleased dev.6 producer.
