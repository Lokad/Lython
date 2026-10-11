# Managed stack admission, 2026-10-12

Stack protection now uses the managed runtime's Boolean stack-admission result,
with the existing logical nesting ceilings. It removes **264 net production
lines**: native Windows/pthread resolution, unsafe pointer sampling, thread-local
range caching, fail-open platform assumptions, the 32-level/96-KiB probe policy,
future-cap prediction and entry-counter rollback. The library and white-box
assembly no longer enable unsafe compilation.

Function/interpreter entries admit before incrementing counters. Recursive
lowered dispatch, structural traversal, copying and public projection likewise
check before descending or publishing state. Explicit configured recursion
exhaustion retains `RecursionError`; interpreter stack refusal reports
`RuntimeError`. Structural/copy refusals retain their recursion error family;
projection retains its projection failure family. Stack admission may refuse
earlier than the old telemetry. Available nesting can vary between startup and
continuation stacks. No new runtime switch was introduced.

The 512-level logical ceiling, cycle checks, host mediation, cooperative
cancellation, allocation preflights, bounded public projection and 16-MiB startup
reservation remain. Forced collection and remaining scalar coupon maps are
separate pending work.

The managed API returns a Boolean; expecting its `Try…` form to throw was not a
sound reason to reject it. A standalone local probe observes real false returns,
but that observation alone does not qualify interpreter unwind safety.
[Microsoft's API contract](https://learn.microsoft.com/dotnet/api/system.runtime.compilerservices.runtimehelpers.tryensuresufficientexecutionstack?view=net-10.0)
describes the admission check.

## Original defects and validation

Original production is `17574145b945b85618f7647954bf24d7cadde9ed`. Seven unchanged
low-stack tests fail on it in Windows Debug/Release and Linux Debug/Release:
function/interpreter entry, pair/single traversal, lowered sync/async dispatch
and public projection. They use real stack pressure on requested 256-KiB threads,
then check state publication and recovery. Original test DLLs, outcomes and
hashes are retained.

An isolated Windows Debug process also overflows natively while comparing 512
nested lists after genuine host I/O. The same frozen producer succeeds at depths
64/128/200/256, and at 512 on its startup stack. Windows Release succeeds at 512
after I/O. These build/stack differences are retained. The replacement turns
the Debug crash into a catchable comparison `RecursionError`; the supported
startup 100-level tail/accumulating recursion and 512-level copy controls remain.
Post-yield recursion at depth 100 already returns a typed error in the original.

Qualified refined candidate source is `3eca95246b153115f878dc96b9176473a0c4fcf8`,
production `3248a5bd4931c6313c49575579ec39144f1e5e46`. Final root producer is
`dde13755eb34ade9c8e123c447ba49a5bd7084af`, with the identical production tree.
Complete matching root Debug suites pass **10,150 checks**: 1,980 white-box and
8,170 public. Private Debug passes 10,206, including 56 temporary outcome-observer
cases that are deliberately absent from the permanent suite.

Windows Release passes 52 focused white-box and 14 public checks. Linux Debug and
Release each pass 22 white-box, 14 public and 56 observer checks, for 184 candidate
checks total. The original has seven expected failures in each Linux build.
Observer success means the process returned a supported result or typed error;
the evidence retains actual guest outcomes instead of treating every observer
as successful guest execution.

Real isolated host-suspension checks exercise deep comparison, recursive
truthiness callbacks, recursive copy hooks, repeated catch/unwind/recovery and
cancellation. Existing recursion, copy, worker-lifetime, structural, cycle,
projection, lowered-body and cooperative execution tests also pass. Individual
diagnostic/test groups have a 30-second bound including cleanup. Full compatibility
suites are separate from performance collection.

The first root integration gate records four fixture loader failures: using
`--no-restore` after adding the fixture's project dependency left stale assets and
missing `Lokad.Parsing` before guest execution. Locked solution restore corrects
the graph; the repeated full root suites pass. The failed gate and diagnostic
remain in the evidence.

## Short diagnostic measurements

Ordinary .NET 10.0.401 and CPython 3.13.16 workers run on the quiet VM with normal
tiering, GC, guards and fresh run contexts. Eight fixed collections cover two
passes of four cases, each taking 7–8 seconds including cleanup. No long lane or
milestone qualification ran. The first wrapper used a stale checkout date and
exited before launching a worker; its failure is retained. Correcting that route
did not change sources, limits or the declared eight measured groups.

| Case | Pass 1 change | Pass 2 change |
| --- | ---: | ---: |
| Empty run | −0.31% | +1.11% |
| Basic integer loop | +1.34% | +2.60% |
| Bounded CSV discard | +7.49% | +0.75% |
| 64-level nested-list comparison | +1.16% | +2.27% |

The loop signal is slower in both passes with low spread (roughly 0.75–0.99% IQR).
CSV spread is broad (4.03–8.25%); its first pass also slows materially. Comparison
changes are small relative to spread: medians move from 56.27 to 56.92 µs and
54.71 to 55.95 µs; CPython medians are 6.51/6.28 µs. All slower results are retained.
This delivery establishes simplification and safety improvements, with no broad
performance or allocated-byte claim. The protocol has no allocated-byte field.

[evidence.json](evidence.json) records source identities, actual DLL hashes,
native crashes, original failures, guest outcomes, test gates, all measured
attempts and owned-process cleanup. All VM groups/PIDs are stopped and empty,
the lease is free, source checkouts remain clean and binaries rehash, including
the preceding delivered set producer. The initial aborted matrix's test DLL was
not retained and its attempted reconstruction did not match; that matrix is not
represented as a frozen paired producer. Refined independent paired producers
establish the exact original crash and matching controls.
