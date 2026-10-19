# Reuse executable import metadata, 2026-10-19 UTC

Delete the duplicate `ExecutableImportBinding` record. Executable tables retain
the existing immutable `ImportStatementSyntax` leaf, so compilation no longer
copies its five fields and async imports no longer reconstruct a syntax node.
The synchronous future-annotations path also reuses it. Direct synchronous slot
binding and genuinely asynchronous host resolution remain unchanged.

Baseline: `75015c9a`, including the preceding module-cache change. Candidate:
that revision plus [the five-file patch](candidate.patch). These frozen source
snapshots compile into a private diagnostic assembly like the white-box suite;
they are not measurements of whichever commit is currently HEAD.

| Boundary | Before allocation | After allocation | Saved |
| --- | ---: | ---: | ---: |
| Compile one entry containing an import | 63,800 B | 63,744 B | 56 B |
| Run 32 cached builtin imports | 25,360 B | 25,360 B | 0 B |
| RunAsync, 32 cached builtin imports | 27,400 B | 25,608 B | 1,792 B |

All three no-import controls have identical allocation medians before and after.
Eight warmup calls, seven samples of eight operations; full raw byte/tick arrays
are preserved. Execution lanes reuse the compiled entry, create a fresh mock host
and dispose execution/results. The mock host completes synchronously; pending I/O
semantics are covered by existing delayed-host regressions. Each operation checks
the shared allowance returns to its prepared baseline. Compilation and process
startup are separate from execution. No forced GC is used.

Windows10.0.26200 X64, i7-14700KF / 28 logical processors; .NET10.0.13,
SDK10.0.300-preview.0.26177.108. This nonquiet diagnostic measures cumulative
managed allocation across threads, not retained heap/RSS or accounting policy.
Elapsed samples are unqualified: no script latency, embedding tail-latency or
CPython speedup is established. No public API or ownership policy changes.

[Scope](measurement-scope.json), [before](before.json), [after](after.json),
[summary](summary.json), [diagnostic](diagnostic.cs), [project](diagnostic.csproj),
[baseline identity](baseline-source.json), [candidate identity](candidate-source.json).
