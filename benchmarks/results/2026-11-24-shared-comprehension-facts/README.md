# B1642 Share comprehension binding facts

Syntax validation, scope analysis, executable compilation and deferred
lowering share each comprehension's ordered iteration bindings. The frontend
removes the second target walk in syntax validation and the scope validator's
separate comprehension walker. Deferred records reference the same facts as
executable code. Nested assignment-expression validation still combines
enclosing targets. Generator ownership of assignment targets is resolved in
the lexical scope that consumes those facts.

The first prototype assumed syntax validation sees every comprehension. Its
frozen worker failed on the generated rules. Lazy type expressions enter
through their own lexical preparation. The corrected path prepares facts at
that first encounter. A CPython 3.13.2-backed regression checks list, dict,
set and generator comprehensions with starred targets and a captured value
that is rebound before the alias escapes. Both execution APIs use it and
return the allowance to baseline. Both platforms pass 892 public and 141
white-box .NET 11 Release checks and 158 public .NET 10 Release checks.

The reference is `50e187921`. Both frozen 32-file images verify the complete
CPython goldens. The generated script compiles 72 lazy rules and checks three
of them plus an eager nested comprehension. The compiler allocates 267,592 B
less per invocation on this workload, down 11.34% from 2,360,728 B.
Configuration manifest and sparse validator compilation allocation each fall
by less than 1%. Retained admission matches for all three, and every compile
returns the allowance to baseline when its owner is disposed.

| Workload | Phase | Elapsed change | Wider CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Generated comprehension rules | Compilation | −15.01 to −14.48% | −16.02 to −13.33% | 0.59 / 0.03% |
| Sparse generated validator | Compilation | −0.33 to +0.63% | −1.77 to +0.46% | 0.03 / 0.93% |
| Configuration manifest | Compile to result | −1.88 to −1.17% | −2.13 to −1.31% | 0.41 / 0.31% |
| Generated comprehension rules | Compile to result | −7.09 to −5.97% | −7.09 to −4.24% | 0.18 / 1.02% |

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on
November 24, 2026 UTC. Ranges span four pairings of worker medians from
seven rotated batches per worker after warmup. Compilation includes
compiled-owner disposal. Time to result also covers execution, applicable
RAM-backed file I/O, verification and prior-result disposal. Process CPU
also covers framing and fixture reset. Startup is outside these clocks.
The generated workload saves CPU in both phases. The sparse control spans
zero. The smaller manifest result is diagnostic. Both quiet gates, all 112
batches, worker joins, fixture cleanup and archive verification pass.

[Identity](identity.json) · [Compilation allocation](compile-allocation-summary.json) ·
[Timings](quiet-summary.json) · [Observations](quiet-observations.tar.xz) ·
[Terminal receipt](quiet-terminal-status.json) · [VM staging custody](retired-stage-custody.json) ·
[Exact patches, failed initial checks and procedures](procedures.tar.xz).
