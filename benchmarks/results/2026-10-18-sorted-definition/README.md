# One immutable sorted definition, 2026-10-18 UTC

The existing sorted callable/signature factory now runs once as a static readonly
definition. Each root uses that definition like the existing thirteen plain
builtins, avoiding per-run construction. Types, exceptions, builtin dictionaries,
modules and arguments/results retain their previous isolation and ownership.
The definition captures no execution, host, borrowed root or guest storage.
Its module self is resolved from the caller. No new cache, owner, scope API,
registry, configuration or iteration/binding mechanism.

Baseline: `75ad9b2eb13933d32b981c624f9bd8327a701ffc`; candidate: that revision
plus the [frozen patch](candidate.patch), including permanent regression coverage.
The [scope](scope.json) and allocation gate precede production edits.

| Warm context construction | Baseline | Candidate | Status |
| --- | ---: | ---: | --- |
| Fresh root | 16,240 B | 16,080 B | All 32 samples improve; 160 B less |
| Child control | 240 B | 240 B | All sample arrays identical |

One native before/after pair reuses the existing context diagnostic with two
warmups and32 measured contexts per route. Builtin inventory103, mutable
int/float/object root isolation, shared child identity, exception aliases and zero
allowance cleanup remain. [Diagnostic](diagnostic.cs), [baseline raw TRX](baseline-native.trx),
[candidate raw TRX](candidate-native.trx), [comparison](comparison.json) and
process receipts retain evidence. The temporary diagnostic was removed.

Local Windows x64 build26200, Intel Core i7-14700KF (28 logical processors),
.NET SDK10.0.300-preview.0.26177.108/runtime10.0.13. [Environment](environment.json)
reuses unchanged configuration with its original capture timestamp. This measures
warmed fresh-context construction, excluding the one-time static initialization,
guest execution, compilation, scheduling, projection and startup. There is no
elapsed-time, compile-to-result, complete-job or CPython speedup claim or retiming.
The fresh-context saving does not establish a gain on the published JSON job.

All 1014 distinct focused Release cases pass, including58 complete CSV/text/JSON/
file jobs, builtin/sort behavior, managed stack, terminal refusal, caches and shared-
root cleanup. Existing readonly/name/hash/module/type isolation checks now include
sorted. A new overlapping sort callback regression pauses one run during host I/O,
completes another with different globals/host, cancels the first and verifies the
other result remains valid and funded. Both then return to compiled baseline/zero.
All10 baseline isolation/callback cases also pass before the runtime change.
[Validation](validation.json) records filters, receipts and TRX hashes.

Before production edits, the new fixture initially used unsupported CLR int[]
globals and therefore failed before the controlled pause. Switching to the
documented object[] form corrects the fixture without weakening assertions or
timeouts; [receipts](fixture-correction.json) are preserved. The initial diagnostic
hash described normalized LF text instead of the actual Windows CRLF file; this
metadata was corrected before edits without changing code or repeating capture.

The fixed definition uses existing bounded trusted runtime metadata headroom,
as documented in [MEMORY.md](../../../MEMORY.md). It stores no call/guest state.
Owned results and caches retain existing scopes; managed safety, cancellation,
preflights and terminal refusal remain. Original Stress scales/assertions/timeouts
are unchanged. Release/platform qualification is recorded in PLAN; regex remains
an unrelated plan-only migration.
