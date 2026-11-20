# Prepared executable lambda bindings

`68215bac8` puts the code object, signature, and flags for generator and
namespace behavior directly in the executable lambda operation. The
operation releases its reference to the lowered expression, default
expressions and parser nodes. Reachable lowered and lazy consumers still
evaluate their defaults and pass the same prepared inputs to function
construction.

The reference is `9b14997e4`. The Windows .NET 11 diagnostic compared
source-identical `5f42f319c` using two independent processes per image.
Each process held five groups of 64 compiled programs with the same source.
The diagnostic measured managed live bytes after full collection. Tiered
compilation was off in both roles. No scripts ran during that measurement.

| Workload | Reference live bytes per compilation | Candidate | Change |
| --- | ---: | ---: | ---: |
| Generated table, 128 lambdas with defaults | 638,200 | 399,608 | −37.39% |
| Stable sort, one key lambda | 9,048 | 8,536 | −5.66% |
| Generated validator control | 258,368 | 258,368 | Flat |

Both process pairs report these medians. The generated table allocates
0.11% more cumulatively during compilation. Warm allocation is flat on
the lambda workloads, and accounted execution peaks match on all three.
The standalone live-byte diagnostic estimates CLR-retained storage. Lython's
coarse admission estimate is separate. Every diagnostic returned the
borrowed allowance to baseline. No elapsed or CPU comparison was made.

Each complete 32-file image matched all three CPython goldens. Windows
.NET 11 and Ubuntu .NET 10 each passed 421 focused Release checks,
including lambda defaults, captures, generators and namespace behavior.

[Summary](summary.json) · [Diagnostics, source and checks](diagnostics.tar.xz) ·
[Exact reference core](../2026-11-20-funded-definition-construction/measured-core.dll.gz).
