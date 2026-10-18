# Ordinary staged text-file writes, 2026-10-18 UTC

This change removes call packaging for exact text-file handles with one ordinary
positional argument. It establishes an allocation reduction in the native
executable interpreter, **not an elapsed speedup or a CPython comparison**.
Baseline: `2b68d4eae329fda6d7f2ed9815755718bce97a50`. The candidate is that
revision plus the [six-file patch](candidate.patch), identified by canonical
source hashes in [validation](validation.json). These are historical diagnostic
identities, not a measurement of an unspecified future HEAD.

| Native interpreter / 128 staged writes | Baseline allocated bytes | Candidate allocated bytes |
| --- | ---: | ---: |
| Ordinary call, synchronous | 15,432 | 11,088 |
| Ordinary call, asynchronous | 42,832 | 11,088 |
| Exposed-method control, synchronous | 15,408 | 15,408 |
| Exposed-method control, asynchronous | 15,408 | 15,408 |

All eight samples per row are identical. [Before](allocation-before.json) and
[after](allocation-after.json) preserve every sample. The
[temporary diagnostic](allocation-diagnostic.cs) was removed from test sources
before final Release builds. No GC was requested, and no unsafe/native mechanism,
pool, cache, owner, writer or benchmark framework was added.

The diagnostic runs an already compiled 128-iteration loop in a fresh funded
context and stages `a\n😀` in an existing text handle. Context/handle/input creation,
compilation, host publication and owner cleanup are outside the thread allocation
counter. Loop/range/interpreter work and governed staging growth are inside.
Async operations must complete successfully without suspension on the same
managed thread. Counts, staged byte position and zero allowance after cleanup
are checked. This boundary excludes public `Run`/`RunAsync` scheduling/setup,
physical host I/O, complete-job latency, compilation and startup.

The active Windows development workstation supplied these allocation samples;
it is not the quiet Azure VM. The diagnostic records allocated bytes, with no
timing, RSS, host-tail-latency or external-baseline claim. Its environment and
source/assembly/process identities are in [validation](validation.json).
No full lane, extra VM capture or repeated elapsed qualification was needed for
this bounded call-transport change. Existing performance comparisons remain in
the main [performance account](../../../BENCHMARKS.md).

The compiler selects the existing receiver/operand convention only for one
positional `write` argument. The runtime directly invokes the extracted original
text-write body only for an exact text-file handle. It omits ordinary bound-method
resolution/capture and call/binding arrays. Exposed methods, keyword/expanded
calls, binary handles and custom/protocol receivers keep general dispatch.
The original writer, encodings, newlines, capacity/replacement preflights, retained
owners, rollback and host publication on flush/close/execution cleanup remain.

Both APIs preserve the baseline's twelve fuel steps and every failure span,
argument effect and staged position. See the [baseline](guard-baseline.trx),
[candidate](guard-candidate.trx) and their process receipts in validation.
Permanent regressions also cover type errors before closed-handle errors,
admission refusal before cursor mutation, and receiver retention/funding while
an argument suspends or cancels. Five public script controls (both APIs) cover
Unicode counts, exposed/expanded calls, receiver rebinding, custom property lookup
order and binary fallback. Their independent goldens also pass isolated
[CPython 3.13.2 controls](cpython-oracle.json); these are semantic checks, not timings.

Focused matching Release checks additionally cover codecs, file lifetimes,
shared allowance/cleanup, retained results/caches, host-output publication,
delayed composition and complete CSV/text/JSON/file script goldens. Counts and
receipts are in [validation](validation.json). Full platform/package qualification
is batched in normal delivery CI. No assertions, deadlines, Stress scales or
containment guarantees were relaxed.
