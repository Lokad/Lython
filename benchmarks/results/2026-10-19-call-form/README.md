# Reuse executable call-argument forms, 2026-10-19 UTC

Delete `ExecutableCallArgumentSpec` and store the existing immutable
`CallArgumentForm` values directly. The same conversion helper still serves
packaged calls and direct operand binding. Keyword names, receiver offsets,
evaluation order and the expanded-call path are unchanged. No public API or
ownership policy changes.

Baseline: `61002a6be430126338cfaf8d99e2292fc21ba3e1`. Candidate: that revision plus
[the three-file patch](candidate.patch). These frozen production sources compile
into a private diagnostic assembly like the white-box suite. They do not describe
whichever revision is currently HEAD.

| Compiled source | Before allocation | After allocation | Saved |
| --- | ---: | ---: | ---: |
| `return max(1,2,3,4)` | 49,536 B | 49,448 B | 88 B |
| `return round(1.2345,ndigits=2)` | 44,304 B | 44,264 B | 40 B |
| `return dict()` | 35,496 B | 35,496 B | 0 B |
| `return 7` | 31,776 B | 31,776 B | 0 B |

Eight warmup calls, seven samples of eight compilations each. Engine and shared
root setup are outside the counters; compilation and disposal are included.
Every operation verifies the root returns to zero. No forced GC is used.
This current-thread cumulative managed allocation metric is neither retained
heap/RSS nor an admission policy. Full raw byte/tick arrays are preserved.

Windows10.0.26200 X64, i7-14700KF / 28 logical processors; .NET10.0.13,
SDK10.0.300-preview.0.26177.108. Process startup, script execution, projection
and I/O are excluded. Elapsed samples on this nonquiet machine are unqualified;
no execution-time, compile-latency or CPython speedup is established.

[Scope](measurement-scope.json), [before](before.json), [after](after.json),
[summary](summary.json), [diagnostic](diagnostic.cs), [project](diagnostic.csproj),
[baseline identity](baseline-source.json), [candidate identity](candidate-source.json).
