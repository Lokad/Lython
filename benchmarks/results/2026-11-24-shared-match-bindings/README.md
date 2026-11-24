# B1648 Share structural-pattern capture bindings

Syntax validation builds one ordered capture map per case. Scope, directive and
flow analysis reuse that map, and lowering uses its indexes for capture slots.
Source spellings distinguish captures that mangle to the same destination.
The first OR alternative orders stores for every alternative. The change retires
three recursive capture collectors, per-capture binding objects and an unused
lowered statement entry. Focused checks cover guards, closures, mangling
collisions, nested diagnostic spans, cancellation and allowance cleanup.

The reference is `83b45a413`. Both frozen 32-file images verify all three
complete scripts against CPython 3.13.2 before the timing run. The classifier
has 48 generated cases with nested mapping, sequence and OR patterns. Its 14
records exercise early and late cases, both alternatives, a false guard and
default cases. Compilation allocates 954,640 B per invocation, down 10.47%
from 1,066,304 B. Retained compilation admission matches. Warm execution
allocation and peak admission match for all three scripts. All measured
compilation and execution owners return their allowance after disposal.

| Complete script | Boundary | Elapsed change | Wider process CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Generated classifier | Compilation | −11.04 to −8.80% | −10.21 to −8.06% | 2.28 / 0.22% |
| Sparse generated validator | Compilation | −0.27 to +1.43% | −2.10 to +4.05% | 1.26 / 0.44% |
| Configuration manifest | Compile to result | −0.37 to +1.21% | +0.33 to +1.00% | 0.27 / 1.32% |
| Generated classifier | Compile to result | −8.84 to −8.05% | −10.83 to −7.54% | 0.72 / 0.15% |

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 24,
2026 UTC. Ranges include all four pairings of worker medians from seven
rotated batches per worker after five seconds of request warmup. Compilation
includes compiled-owner disposal. Compile to result adds execution, output
verification, previous result disposal and mediated RAM-backed I/O where
applicable. Process CPU also includes fixture reset and framing. Startup is
outside these clocks. The manifest has a small adverse CPU result while its
elapsed range crosses zero. Execution allocation and admission match, and
the run does not isolate a cause or measure a warm timing improvement.

Windows and Ubuntu each pass 1,753 focused .NET 11 and 55 .NET 10 Release
checks. Both quiet gates and all 112 batches pass. Every worker joins and
the fixture mount is removed before verified archival.

[Images](identity.json) · [Compile allocation](compile-allocation-summary.json) ·
[Execution allocation](allocation-summary.json) · [Timings](quiet-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Terminal receipt](quiet-terminal-status.json) ·
[Source patch, tests and procedures](procedures.tar.xz).
