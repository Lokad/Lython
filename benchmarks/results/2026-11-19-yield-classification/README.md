# R689 callable generator classification

Yield validation now records which function or lambda owns a suspension.
Lowering consumes that fact and the declaration-body classification scans
are removed. The validation still assigns yields in callable defaults and
class headers to their enclosing function. It preserves lambda bodies and
the first comprehension iterable as distinct evaluation boundaries.

The allocation comparison uses `c577a7662` and `8f20d4168` on local Windows
.NET 11 RC3 on November 19, 2026 UTC. The measured change was retained
at `0f52a1088`.

| Compile workload | Reference bytes | Candidate bytes | Change |
| --- | ---: | ---: | ---: |
| Ordinary rules | 84,080 | 83,848 | −0.28% |
| Annotated rules | 65,152 | 64,912 | −0.37% |
| Generator rules | 97,040 | 96,608 | −0.45% |
| Retained sibling functions | 118,552 | 117,608 | −0.80% |
| Nested closures and generators | 98,464 | 98,032 | −0.44% |
| Generated validator | 3,765,644 | 3,742,833 | −0.61% |
| Configuration manifest | 327,976 | 324,352 | −1.10% |

Medians use five current-thread allocation observations during public
compilation and compiled-owner disposal. Each invocation restored the
borrowed allowance to baseline. Elapsed and process CPU remain unmeasured.
The initial summary looked for a separate results file. The diagnostic
had written complete observations to stdout, and the archive uses that
output from the original run.

Windows .NET 11 and Ubuntu .NET 10 each passed 488 focused Release checks
covering generator semantics, defaults, postponed annotations,
comprehensions and cancellation. The complete 32-file candidate image
passed five CPython output and file goldens.

[Samples and identities](summary.json) ·
[Source and checks](checks-and-procedures.tar.xz).
