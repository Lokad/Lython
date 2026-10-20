# Compilation-owned host requirements

Runtime `0ed30179` collects immutable host requirements once during compilation,
using the executable module's existing binding facts. Before execution it samples
the two capability flags and filters facts through the existing four-state cache.
The host-dependent syntax rewalk, duplicate `StaticAnalysisContext`, orphaned
contract helper and redundant raw-script branch disappear. Original syntax still
serves module annotation initialization; this does not claim that all syntax is dead.

Metadata list capacity/replacement and final-array overlap are admitted through
the compilation owner before allocation. Only the completed array is committed;
scratch unwinds on success, cancellation or refusal. The existing compilation
estimate covers the diagnostic cache. Neither facts nor cached diagnostics retain
the compilation token. Retained metadata can conservatively reject compilation
earlier. No new allowance, tracing, quota, storage framework or public option.

The confirmed false stdin requirement for user-defined `input()` is fixed. Any
module binding/global directive for `input` makes direct calls uncertain throughout
that source, including before binding/after deletion. Those calls now use the
existing runtime check: earlier guest effects can precede its missing-stdin
`RuntimeError`. Unshadowed builtin calls retain pre-execution rejection. General
host-global or module/member shadowing resolution is not claimed. Runtime host
authorization remains mandatory.

[Nine original failures](red.txt) cover module function/lambda/alias/callable
bindings and conservative runtime-failure timing. The unshadowed builtin control
already passed. Focused Release passes: 174 public diagnostics/capability/budget
checks, 10 white-box storage/cancellation controls and 149 complete representative
scripts, alias and retained-file checks. These are overlapping selections, not a
unique total. All four capability states retain ordered diagnostic codes/spans
under both APIs. Cancellation after metadata admission unwinds private storage;
refused replacement retains old backing until cleanup; retained facts remain
charged. No assertion, timeout or budget was relaxed. [Selected receipts and
excluded invalid attempts](checks.json), [six independent CPython 3.13.2 binding
goldens](oracles.json) preserve the evidence.

One short diagnostic reuses the maintained `scripts.config-edit.small` source and
fixtures unchanged: read JSON configuration, update fields and paths, write exact
output (16 input paths in 181 bytes of JSON). Each invocation freshly compiles
the guest source and obtains its first
synchronous result in an already-running host process. This exercises compilation
and first-use host checks rather than a warm-only execution kernel.

| Engine | Fresh compile-to-result | IQR / median | Status |
| --- | ---: | ---: | --- |
| Lython `a017d12a` | 2.131 ms | 23.16% | Before; diagnostic |
| Lython `0ed30179` | 2.349 ms | 12.15% | After; diagnostic |
| CPython 3.13.2 | 1.963 ms | 6.61% | External baseline; diagnostic |

The nominal 10.2% higher candidate median is within the observed Lython spreads.
The result is inconclusive: no qualified speedup, regression, allocation or
embedding-latency claim. The after/reference ratio is about 1.20 (Lython elapsed /
CPython elapsed). No retiming or full lane.

Measured 2026-10-20 UTC on shared LOKAD-0399, Windows build 26200, x64/28 logical
processors, .NET 10.0.13/workstation GC and ordinary CPython GIL/GC. Guest compilation,
first execution, compilation/result cleanup, disk fixture staging/publication and
exact output checks are included; library build, process startup and transport
are excluded. The timing root is explicitly unbounded. Seven alternating rounds
follow warmup. Parent and workers finish in 7.727 seconds without forced GC.
[Declaration](declaration.json), [frozen before identity](baseline.json), [catalog](catalog.json),
[raw samples/loaded identities](micro.json) and [process receipt](micro.receipt.json)
retain the boundary and file/source/golden hashes.

Full Windows/Ubuntu Release, original Stress and both explicit package consumers
pass at exact runtime revision `0ed30179` in [CI 39350313310](https://github.com/Lokad/Lython/actions/runs/39350313310).
[Every required final step](delivery.json) was inspected. Three spaced status
queries; terminal green is cached forever and must never be queried again.
Publication remains deferred.
