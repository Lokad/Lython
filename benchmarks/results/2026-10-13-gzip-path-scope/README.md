# Path-based gzip handles release their creation scopes

The change in `8b160695178f76f3258b256914de291b0c09ba70` replaces the lexical
`ExecutionContext` retained by `GzipFileHandle` with existing `ExecutionServices`.
Before revision: `20524d7b26a0c4e3e3c2366855ded6e55c6b46dc`, production tree
`9c68d231f03f6190a9119a5c078d2951854019a8`. After production tree:
`953de207c862181e032bb6063eb65859919ce4bd`.

An escaping `gzip.open` handle previously pinned an unrelated 16,000-byte local
in its opening function, even after close. All twelve binary/text read, write
and append × open/closed owner probes failed before the change and pass after it.
The handle still retains its actual decompressed content or staged history.
There are no new accounting owners, reclamation rules or configuration switches.

| Validation | Result | Scope |
| --- | --- | --- |
| Before: creation-scope owner probes | 12 failed | Weak creation frame remains live beside escaping handle |
| After: same owner probes | 12 passed | Frame/local collect; fitting admission and retained read bytes succeed |
| Focused white checks | 30 passed | Includes those 12 and existing gzip ownership/rollback checks |
| Public lifetime controls | 24 passed | Six modes, synchronous/genuinely delayed async I/O, dropped/retained aliases, fixed 28,000-byte cap |
| Full Release suites | 10,829 passed | White-box and public suites, including existing gzip/codec/host/cancellation coverage |
| Exact Release package consumer | Passed | Explicit Release pack at 8b160695; isolated compatibility and mediated file checks |

These are correctness checks on Windows/.NET 10.0.12 Release with ordinary
GC/tiering, run on 2026-10-13. The weak-reference checks explicitly collect to
verify ownership; they do not measure runtime relief or embedding latency. No
CPython timing, throughput or reduction in production collections is claimed.
Original finite process receipts, TRX, source and binary hashes are preserved in
the private `.git/agent-notes/gzip-path-scope-20261013/` evidence directory.
The checked NuGet package has SHA512
`0953D60F8503D520D8B9FC5E38DC8E6E97C52D260A450725E13398CC532FD85B0889437611ADF0F1CC491797F689A41A0DEDFBD1C54721D43926B19AB0DE14A3`.

The [white checks](../../../tests/Lokad.Lython.Tests/GzipPathScopeLifetimeTests.cs)
verify collection, live-cap admission and retained content. The
[public checks](../../../tests/Lokad.Lython.PublicApi.Tests/GzipPathScopeLifetimeTests.cs)
also independently decompress produced files with the BCL and verify exact bytes.
Existing full-suite gzip tests cover cancellation, codecs, append, host failures
and failed-close retries. CRC and compression reuse the same governor and guard
implementation through shared services; guest size coercion and `writelines`
still use the invocation's caller context.

This does not change callback-based `gzip.GzipFile`: that wrapper invokes guest
stream methods and numeric conversion. Its caller/root lifetimes require a broader
representation. General reference-withdrawal recovery and reclamation without
forced CLR collections also remain open; see the
[ownership assessment](../2026-10-13-embedding-exception-pressure/ARCHITECTURE.md).
