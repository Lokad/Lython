# B1875 Attribute generated Python compilation

The November 27 Windows managed-stack trace used the frozen `6e59cd41e`
worker and .NET 11 RC3. The only source differences at inspected `50392d6fa`
were the CSV dictionary writer and JSON emitter. The three scripts use
neither. Frozen images and three independent CPython 3.13.2 goldens
verify. Both definition scripts contain 96 nested functions and lambdas.
One returns three callables. A matched variant returns all 96 and invokes
the same three. The third script compiles and runs 96 guarded row rules.

| Workload | Phase | Verified commands | Abstract analysis¹ | Emission and finalization¹ | Parsing¹ |
| --- | --- | ---: | ---: | ---: | ---: |
| Ordinary definitions | Compile only | 8,704 | 27.6% | 23.8% | 17.7% |
| Escaping definitions | Compile only | 9,600 | 26.8% | 23.3% | 18.9% |
| Guarded report | Compile to result | 4,864 | 17.0% | 32.6% | 22.9% |

¹ Share of sampled managed-stack time with a resolved compilation ancestor.
The guarded report percentages describe its compile portion. The capture
uses warmed comparison workers and six-second EventPipe windows. Sampled
managed stacks identify where to inspect the code. They do not establish
whole-command CPU shares or attainable speedups.

The definitions spend sampled time in binary diagnostics, call analysis
and growth of the abstract binding and expression-resolution maps. The
guarded report shows instruction-list growth during emission. First test
whether identifier resolution can use the existing flow binding without a
second cache entry. Keep computed results and mutation invalidation intact.

The quiet VM failed before sampling when creating a directory on the nearly
full disk. Archive-verified duplicate files were compacted, but available
space fell again before staging. The original failed launch, retry preflight
and custody record are in the archive. All local batches and worker closes
verify. The trace archive includes the original EventPipe recordings.

[Attribution](attribution.json) · [Image identity](identity.json) ·
[Run summary](run-summary.json) · [Observations](observations.tar.xz).
