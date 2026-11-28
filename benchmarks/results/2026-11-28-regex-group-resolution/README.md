# B1931 Complete ordinary regex group selection directly

Exact integer and string selectors now give the resolved group to the
shared projection immediately. Scalar group reads and subscripts no
longer start an unconditional async state machine for selection or result
projection. The same selector serves `start`, `end` and `span`. Guest
index, hashing and equality still support suspension, and a multi-group
tuple is built in the original order. Group text still uses the original
subject and the existing admitted materialization.

The reference is `c669b6c4b` and the corrected candidate is `081f6b831`.
Only the Lython core DLL differs in the 32-file worker images.
Both use the same optional adapter and signed PythonRe 0.5.0. Both
match five independent CPython 3.13.16 output and file goldens. The
retained-group script checks the group text after the iteration has ended.

## Allocation and ownership

| Script | CLR allocation saved per execution | Peak admitted bytes |
| --- | ---: | ---: |
| Compiled log search | 114,640–114,688 B, about 7.2% | 1,178,381 |
| Unicode named groups | 516,096 B, about 22.6% | 2,089,021 |
| Retained Unicode groups | 0 B | 2,664,413 |

Windows .NET 11 uses the existing allocation witness through both APIs.
All five scripts match the reference peak in both modes and return the
borrowed allowance to baseline. The first candidate allocated two
closures for async tuple construction, adding 172,032 B per retained
Unicode group workload. Both tuple paths now create their closure in the
shared tuple helper. Scalar group reads allocate no tuple closure.

Windows .NET 10 and 11 and Ubuntu WSL .NET 11 each pass 581 public and
916 white-box focused Release checks. These include real suspension in
guest selection callbacks, cancellation, terminal refusal, cleanup and
retained results.

## Warm timing

The corrected run used Ubuntu WSL and .NET 11 RC3 on November 28, 2026,
pinned to one logical CPU.
Each of two workers per image warms for at least four seconds and records
seven rotated batches. Ranges span all pairings of worker medians.
Elapsed includes mediated RAM-backed I/O, verification and prior-result
disposal. Process CPU also includes fixture reset and request framing.
Compilation and startup are outside both clocks. All 168 batches pass in
each run, both archives verify and all workers and fixtures clean up.

| Script | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Compiled log search | `Run` | −3.77% to −3.12% | −4.59% to −0.54% |
| Unicode named groups | `Run` | −21.93% to +22.89% | −19.41% to +18.15% |
| Retained Unicode groups | `Run` | −6.08% to +8.29% | −4.17% to +7.79% |
| Unicode named offsets | `Run` | −1.89% to +3.35% | −1.78% to +1.19% |
| First-line search control | `Run` | −4.49% to −0.98% | −5.99% to −3.95% |
| Compiled log search | `RunAsync` | −4.38% to −3.20% | −3.45% to −1.31% |

The corrected log timings improve in both APIs, and the unrelated
first-line control improves by a comparable amount. Named-group worker
medians vary widely in both runs. The initial run also has inconclusive
async log timing. Retain the simpler path and the measured reduction in
allocation. Leave the CPU gain unresolved for the next representative
quiet comparison.

[Corrected timing](warm-summary.json) · [Initial timing](initial-summary.json) ·
[Allocation](allocation.json) · [Profile attribution](group-costs.json) ·
[Image identity](identity.json) · [Corrected observations](warm-observations.tar.xz) ·
[Initial observations](initial-observations.tar.xz) ·
[Exact patch and checks](procedures.tar.xz).
