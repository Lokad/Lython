# Caller-bound gzip wrapper lifetime

Runtime revision: `9dcbf6fcaa480a642d6727f656d74578f2a93c3a`; production tree
`325120a60aa8fe80bc38c8161061958590f6bb65`. Before: immutable Release package
from `7b8b0bec`, production `7bbf8219`. These are correctness checks on
Windows x64/.NET 10.0.12 Release, 2026-10-13, with ordinary settings. No new
throughput or embedding-latency claim is made.

Callback-based `gzip.GzipFile` permanently retained the lexical context that
constructed it. An escaped reader or writer, including a closed one, therefore
kept unrelated creation locals alive. Its methods also ignored the caller
already supplied by callable invocation. Dropped data could falsely consume a
later memory admission even though the wrapper did not need it.

The wrapper now retains shared execution services and binds its caller for the
active operation only. This field replaces the independent busy flag: entry
rejects reentrancy, and completion or failure withdraws the caller. Pending I/O
keeps the caller for its full operation. Native compression callbacks use shared
services; stream callbacks and guest conversions use the active caller.

Iteration and context management reuse Python special-method invocation. One
internal native-protocol marker admits this built-in iterator without treating
arbitrary module attributes as Python type protocols. Existing intrinsic iterator
fast paths stay intact. The old context-free gzip iterator/context-manager
wrappers are removed. Writer shutdown receives the run context explicitly,
including error publication; no permanent surrogate lexical frame or ambient
caller registry is added.

| Validation | Before | Candidate |
| --- | --- | --- |
| Creation-scope controls: reader/writer, open/closed, retained payload aliases | 8 failed | 8 passed |
| Completed/failed operation withdraws its caller | — | 2 passed |
| Public recovery/retention at 28,000 bytes, identical test DLL | 4 passed / 4 failed | 8 passed |
| Public protocol routes, Run and RunAsync | — | 20 passed |
| Complete Release suites | — | 10,887 passed |
| Exact isolated Release package consumer | — | Passed at 9dcbf6fc |

The [white controls](../../../tests/Lokad.Lython.Tests/GzipCallbackScopeLifetimeTests.cs)
validate weak creation/caller references while keeping the actual gzip owner and
raw stream alive. Explicit payload aliases remain alive and funded. The
[public controls](../../../tests/Lokad.Lython.PublicApi.Tests/GzipCallbackScopeLifetimeTests.cs)
cover direct next, for/list, generator acquisition, a user iterator returning the
native wrapper, with, escaped bound methods, gzip-to-gzip writelines, reentrancy
and closed iteration. Read callbacks perform actual host I/O; delayed-host cases
verify asynchronous completion, except closed-admission controls which perform
no I/O. Existing cancellation, retained buffers, constructor denial, exhausted
cleanup and finalization tests remain required.

The identical public test assembly SHA256 is
`F8585666DEDB95BCFF72E70F2F57AD22D8EE99DF8A90D35B22A031A89F3E487B`.
The before package SHA512 is
`C7B1B95CA29920353623EAFABB39D176820663E25465290C648B42C5AFA72250C712C12381EE9D5D89A8897DC97E04392000853F75BDC9AAA985D21923F91CEC`.
Its original bytes were verified and remain unchanged; only a separate test
bundle receives its DLL. Candidate package SHA512:
`BE4E630A33BDAA1705F55E5C3E1D8A68930CF4D581268615CDFA733BAE4469D3C55C0E5741D3E09188260D9321D840A60B750FB3F074CC1791B47D1DC9BD5AB0`.

The broader diagnostic batch exceeded its 28-second bound and was stopped;
its partial passes do not constitute a successful gate. Original failed builds,
before failures, timeout and all subsequent receipts/TRX are preserved privately
in `.git/agent-notes/gzip-caller-20261013/`. The narrow candidate protocol group
passes 28 cases in a 4.705-second owned process including cleanup. Test duration
does not measure runtime performance.

The routing also corrects closed iterator admission (ValueError, checked against
local CPython 3.13.2) and rejects context-manager exit reentrancy during callbacks.
The [specification](../../../SPEC.md#1123-sequential-gzip-binary-wrappers) records
the resulting lifetime contract. Allocation preflights, exact live alias funding,
finalizable storage accounting, cancellation and managed stack admission remain.
Complete root/helper/child/resource representation and reclamation without forced
CLR collection are still open; see the
[ownership assessment](../2026-10-12-memory-roots/OWNERSHIP-RULE-2026-10-13.md).
