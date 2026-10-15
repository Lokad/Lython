# Protected file-search loops: executable coverage and cleanup

Ordinary `break` and `continue` inside `with`/`try` now use existing executable
region handling instead of rejecting the enclosing module/function compilation.
The realistic file-search case exposed a correctness defect: threading an abrupt
jump through its destination could cross the protected-region boundary, run
cleanup early and discard the loop iterator twice. Preserve that destination
during reachability/normalization; ordinary jump threading remains unchanged.
No new runtime dispatch or cleanup workaround is introduced.

## One comparison per execution boundary

| Boundary | Lython b2235259 | Lython candidate | CPython 3.13.16 | Candidate IQR / median |
| --- | ---: | ---: | ---: | ---: |
| Warm complete job | 3.004 ms | 4.249 ms | 0.265 ms | 56.4% |
| Fresh compile and complete job | 6.715 ms | 6.041 ms | 0.395 ms | 42.4% |

These are inconclusive diagnostics, **not a speedup or a qualified CPython
multiplier**. Baseline spread is 12.6% warm and 78.7% compile/run; CPython spread
is 0.2% and 2.2%. All seven rotating rounds, calibration and warmup remain in
[warm.json](warm.json) and [compile-run.json](compile-run.json). Each collection
ran once, taking 8.268 and 8.212 seconds including owned cleanup, under separate
30-second external deadlines. No timing samples were replaced or retried.

The workload is [file-search.py](../../scripts/file-search.py): one 42,010-byte
UTF-8 file with 2,049 lines, a helper predicate, early exit after line 1,025,
and exact output-file/stdout checks. Empty and small variants are also checked.
Both worker timers include file reset/staging, invocation initialization,
script I/O, output capture/projection and exact byte checks. Warm excludes
compilation; compile/run creates fresh code, executes it once and relinquishes
the temporary compilation inside each timed invocation. Both exclude process
startup, transport and full response hashing. The async path is correctness
tested separately; these measurements use synchronous execution.

Measured 2026-10-15 on the dedicated Azure Standard_F4as_v7: Ubuntu 24.04.4 LTS,
four AMD EPYC 9V45 vCPUs, 16 GiB RAM, SDK 10.0.401/runtime 10.0.12, and
CPython 3.13.16 with ordinary GIL/GC and `-I -S -X utf8`. Workers retain ordinary
GC/tiering and public Lython policies with a shared unbounded allowance.
CPython receives no artificial containment limit.

## Source and correctness evidence

The baseline is b22352597231c50d1738ebf3f88107d6ba1f3fe0. The candidate was built
before commit from that revision plus **only two frontend production changes**.
Its informational version still names the parent; do not describe it as a
measurement of an arbitrary later HEAD. The same updated benchmark harness,
micro lane selection and script/catalog were used for both roles.
[The declaration](comparison-declaration.json) freezes source bytes;
[source/cleanup proof](cleanup-source-proof.json) confirms the two production
differences, identical comparison sources, exited workers, removed fixture
directories and released lease. Loaded DLL/toolchain hashes are in both raw
timing receipts. No profile or longer qualification lane was collected.

Seventeen [independent CPython 3.13.2 programs](cpython-goldens-region-regression.json)
cover internal/exiting jumps, nested managers/loops, handler cleanup, pending
return/exception precedence, exit suppression and terminal for/while/generator
breaks. Public tests compare stdout and cleanup-file bytes in synchronous and
genuinely yielding execution, with shared allowance returning to baseline.
Additional tests preserve rejection before a denied value, earlier published
writes and cancellation during pending exit. Structural checks require
executable helper bodies and no fallback/mirroring in the ordinary search loop.
[Baseline](baseline-correctness.json) and [candidate](candidate-correctness.json)
VM workers independently match CPython for all three search sizes.

Release delivery gates pass 10,881 ordinary and 377 stress tests;
[validation.json](validation.json) preserves counts, elapsed bounds and TRX hashes.

Stopping criterion: executable coverage, correct cleanup and containment gates;
retain the simpler compiler and correctness fix without an elapsed benefit claim.
Async function-construction parity and comprehension fallbacks remain separate
work. These results do not establish their cost or resolve the overall gap.
