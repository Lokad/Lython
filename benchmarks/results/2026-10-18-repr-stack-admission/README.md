# Recursive repr stack admission, 2026-10-18 UTC

This correction supersedes unsafe intermediate `8f00a1c3`. Its ordinary CI was
green, but a bounded subprocess witness with 25,000 nested lists overflowed the
CLR stack. The exact immutable Release package reproduces exit 0xC00000FD; the
preceding b0ac7c60 package returns MemoryError for that input. Cycle detection
does not bound an acyclic graph. A changed native frame layout exposed missing
admission in recursive rendering; fewer allocations did not prove containment.

Move the existing managed Enter/LeaveInterpreterFrame pair from the repr root
into its recursive core. Every value descent now checks cancellation/fuel, the
existing 512-level interpreter ceiling and managed stack headroom, including
after real asynchronous I/O. The root-only placement disappears. No new policy,
counter, option, native probing or explicit VM frame subsystem is added. Lazy
cycle storage, existing traversal/finally removal, preflights and owners remain.

Baseline: `8f00a1c343e0666ec44a3a017605b405a2a7a33a`; candidate: the
[one-file patch](candidate.patch). Both corrected API subprocesses exit normally
with the existing attributed stack-limit RuntimeError. The permanent fixture
uses 1,024 nested values rather than 25,000: lists, tuples and dictionaries fail
safely, unwind, return allowance to baseline and allow a shallow next execution,
under both APIs and after real host I/O. Three native tests prove admission at
the immediate child entry. These nine cases supplement existing cycle tests.

The prior [frozen allocation capture](../2026-10-18-repr-cycle-storage/candidate.trx)
is reused as baseline; there is one new capture, not a new measurement campaign.
All 34 allocation/funding/output arrays match. The earlier 64 B scalar saving is
retained. All 190 scalar fuel records and 48 scalar admission records match;
all 64 admission outcomes/errors/charges match. Container children now consume
one checkpoint each, so 114 container fuel records intentionally change:

| Existing measured container shape | Additional checks per call |
| --- | ---: |
| List, tuple, dict, nonempty set | 2 |
| Self-referential list | 1 |
| Tuple/list cycle | 2 |
| Cyclic dict-values view | 3 |

These are safety checkpoints, not removed work. Exact historical fuel thresholds
and the point of deep-operation failure change; a stack failure can precede
memory refusal. The existing managed RuntimeError policy differs from CPython's
RecursionError for deep repr. Shallow results, aliases and cycle spellings remain.

All 336 distinct focused Release checks pass: 46 white-box, 138 public rendering/
protocol, 90 admission/ownership and 62 complete-job/cancellation cases (58 complete
CSV/text/JSON/file jobs). Six independent CPython 3.13.2 programs match both APIs.
Original Stress scales, assertions and timeouts remain. Temporary diagnostic
removed. [SPEC](../../../SPEC.md) documents per-value admission/fuel semantics.

Local Windows 10.0.26200 X64, i7-14700KF/28 logical processors, 68.4 GB;
SDK 10.0.300-preview.0.26177.108, runtime 10.0.13. Allocation capture excludes
compilation, startup scheduling, projection and I/O. No elapsed or CPython speedup
claim. Direct-native diagnostic rows are synchronous under either harness mode.

[Scope](measurement-scope.json), [diagnostic](diagnostic.cs), [candidate](candidate.trx),
[comparison](comparison.json), [original fault](original-fault.json),
[exact-package/correction proof](correction-proof.json), [source crash trace](source-fault.stderr.gz),
[package crash trace](package-fault.stderr.gz), [sync correction](corrected-sync.json),
[async correction](corrected-async.json), [CPython probes](candidate-probes.json)
and [validation](validation.json) preserve the evidence. Full traces are losslessly
compressed; their uncompressed hashes are in the fault records. Platform/package
qualification is recorded in the private plan. Regex remains planning-only.
