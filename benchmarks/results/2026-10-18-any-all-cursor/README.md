# Async any/all cursor consumption, 2026-10-18 UTC

The two general async aggregate bodies consume the existing PyIteration.Cursor
directly and await its disposal. Their AsyncSequence/AsyncEnumerator wrappers and
per-pull forwarding disappear. The existing cursor keeps iteration protocols,
checkpoints, dictionary-version rules, native cleanup and guest-iterator retention.
Sync and exact bytes/range routes stay unchanged. No cursor/framework, owner,
cache, binding, preflight or memory-contract changes; sum is outside this change.

Baseline: `7c79191f03d0fcd6f9dbd81d4d68f6c1c61b284d`; candidate: that revision plus the
[frozen patch](candidate.patch), including permanent regression coverage.
The [scope and gate](scope.json) precede runtime edits. The measured source is
final; one before/after allocation pair, with no repeated capture or tuning.

| Native helper route, both any and all | Baseline | Candidate | Status |
| --- | ---: | ---: | --- |
| Async list, empty/full/early exit | 248 B | 152 B | 96 B less per call |
| Async empty tuple | 208 B | 112 B | 96 B less per call |
| Async 65-item tuple, full/early exit | 240 B | 144 B | 96 B less per call |
| Sync list control | 176 B | 176 B | Identical arrays |
| Async bytes control | 24 B | 24 B | Identical arrays |
| Async any(range(0,1)) control | 256 B | 256 B | Identical arrays |
| Async all(range(1,66)) control | 2,304 B | 2,304 B | Identical arrays |

Eighteen routes use two warmups, eight samples and 64 calls per sample. Empty,
65-item full-drain and first-item short-circuit routes all improve. Every array is
stable; all 68 exact existing work/fuel/error/span/admission records are identical.
[Diagnostic](diagnostic.cs), [baseline allocation TRX](baseline-native.trx),
[candidate allocation TRX](candidate-native.trx), [baseline behavior TRX](baseline-work.trx),
[candidate behavior TRX](candidate-work.trx), process receipts and
[comparison](comparison.json) preserve the evidence. Temporary diagnostic removed.

Local Windows x64 build 26200, Intel Core i7-14700KF (28 logical processors),
.NET SDK 10.0.300-preview.0.26177.108/runtime 10.0.13. [Environment](environment.json)
reuses unchanged configuration with its original capture timestamp. Allocation
calls assert synchronously completed ValueTasks: this excludes actual suspension,
host I/O, scheduling, startup, compilation and public projection. No elapsed,
whole-job, compile-to-result or CPython speedup is established.

All 354 distinct focused Release checks pass, including 58 complete CSV/text/
JSON/file jobs. Existing truth/iterator callbacks actually suspend, cancel and
retain owners. New 16 paused native cleanup cases keep the aggregate pending and
its scratch funded until disposal completes, exactly once, through short circuit,
exhaustion, pull failure, cancellation, refusal and cleanup failure. Six new public
cases pin retained-generator resumption and list/dictionary mutation. Three
independent CPython 3.13.2 programs supply [goldens and baseline probes](goldens.json).
All 22 new rows pass before the runtime edit. [Validation](validation.json)
records filters, receipts, counts, source hashes and unchanged cursor sources.

Initially six cleanup fixture rows expected raw CLR exception identity across a
guest call. That boundary translates InvalidOperationException to RuntimeError;
the corrected test pins exact type/message. Pause, funding, exactly-once cleanup,
timeouts and route assertions remain. [Initial receipts](fixture-correction.json)
precede production edits; this was a test expectation defect, not a runtime bug.
Original Stress scales/assertions/timeouts remain. Release/platform qualification
is recorded in PLAN; regex remains planning only.
