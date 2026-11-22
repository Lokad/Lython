# B1395 Merge compiler assignment facts from branch edits

The quiet Azure F4as_v7 compared frozen `c097d73db` and `d5fa8c906`
on November 22, 2026 UTC with Ubuntu 24.04.4 and .NET 11 RC3. The
frontend patch was retained at `f31edc41b` with the independent
regex template change already present.

Static name diagnostics use one current assignment set, a reusable
undo log, and lists of names to join. Read-only branches no longer
copy incoming sets or merge their unchanged members. Sibling branches
still start with the same incoming state. Guards can update the facts
seen by subsequent match cases, and comprehensions restore their
private targets while preserving assignment-expression bindings.

| Workload | Boundary | Elapsed change | Wider CPU change | Worker spread reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Sparse generated validator | Compile | −7.61 to −5.16% | −8.72 to −3.54% | 0.65 / 2.00% |
| Generated function with 384 bindings and 180 read-only branches | Compile | −36.40 to −35.11% | −36.91 to −35.18% | 0.48 / 1.54% |
| Inherited configuration dataclasses | Compile | +0.67 to +2.38% | +0.45 to +2.03% | 0.44 / 1.26% |
| Postponed nested factories | Compile to result | −1.41 to +5.85% | −0.84 to +6.69% | 1.49 / 5.78% |
| Sparse generated validator | Compile to result | −3.55 to −0.32% | −1.94 to −0.25% | 1.73 / 1.58% |

Two independent processes per source warm for eight seconds and
complete seven rotated batches each. Ranges span pairings of process
medians. Compilation includes prior compiled-owner disposal. Time to
result includes execution, verified output and cleanup of result and
compiled owners. These scripts use no guest file I/O. Wider CPU
includes request handling. Process startup and final teardown are
outside these clocks.

Local compile allocation drops about 5.4% for the sparse validator and
21.70% for the generated branch stress fixture. Dataclass allocation
matches and remains slower elapsed and CPU across the measured pairings.
Postponed-factory timing spans zero. Retain the branch simplification
for the affected workloads and keep these findings open before
publication. The branch stress fixture is a compiler diagnostic.

Both platforms pass 1,017 focused Release checks on the source pair.
Fifteen independent sources produce identical instructions, stack
layouts and source spans. All 21 diagnostic cases agree on messages,
order, spans, retained bytes and admitted peaks. Owners refund. Both
frozen images match four CPython goldens. All 140 quiet batches and
both machine gates pass, and the archive verifies after joined workers
and removal of guest fixtures.

[Sources and images](source.json) · [Timing](summary.json) ·
[Allocation](workload-allocation.json) · [Compiler cases](compiler-allocation.json) ·
[Raw observations](observations.tgz) · [Checks and procedures](procedures.tar.xz).
