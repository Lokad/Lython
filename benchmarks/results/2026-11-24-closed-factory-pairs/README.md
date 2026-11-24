# B1594 Complete a closed callable pair directly

Private `611468350` uses the closed-default proof for lexical `def` and
lambda definitions in a two-item return. It admits each definition's
default storage in source order, creates fresh callables and writes the
pair into an existing caller-owned unpack window. This removes the
zeroed callee workspace, local definition stores and tuple preparation
for an eligible call. Other calls keep Python's ordinary path.

| Complete script | Boundary | Elapsed change | Wider process CPU change | Elapsed spread reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Captured rules | Warm | −4.37 to +3.62% | −4.02 to +3.07% | 2.41 / 5.79% |
| Defaulted rules | Warm | −35.87 to −32.16% | −36.17 to −32.45% | 5.73 / 0.05% |
| Eager annotated rules | Warm | −5.18 to +0.21% | −5.49 to +0.19% | 3.47 / 2.13% |
| Postponed annotated rules | Warm | +2.08 to +7.90% | +2.35 to +8.39% | 2.72 / 2.90% |
| Short Unicode report | Warm | +0.38 to +4.24% | +2.59 to +5.32% | 1.51 / 2.30% |
| Quoted invoice | Warm | −3.14 to −0.23% | −4.44 to 0.00% | 1.88 / 1.11% |
| Defaulted rules | Compile to result | −31.81 to −30.20% | −31.56 to −29.28% | 2.25 / 0.11% |

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on
November 24, 2026 UTC. Two workers per image ran seven rotated batches
per script and phase after eight seconds of warmup. Ranges cover all
pairings of independent process medians. Warm uses compiled code and
includes applicable mediated RAM I/O, goldens and prior-result disposal.
Compile to result adds compilation and compiled-owner disposal. The wider
CPU clock also includes request and fixture handling. Startup and final
worker teardown are separate. The 32-file `04f87924c` and `611468350`
images differ only in the core DLL.

Keep this candidate private for refinement. Defaulted rules improve in
both phases at matched managed allocation and admitted peak on all six
scripts. The postponed and text controls move adversely, with CPU support.
A Windows FullOpts diagnostic with tiering disabled shows the shared
completion wrapper at 1,106 versus 797 bytes and the computed definition
constructor at 1,700 versus 1,134. The original workspace body remains
549 bytes. These generated sizes suggest keeping generalized pair work
separate from the existing single-definition setup and measuring the same
controls again. They do not identify the cause of the text movement.

A separate instrumented core counts 8,192 direct factory pairs across
four defaulted invocations and zero native workspaces. The count covers
two verification invocations and one invocation on each execution API.
Captured, eager, postponed, text and invoice scripts use zero such pairs.
Both platforms pass 57 focused white-box and 152 public .NET 11 Release
checks. The cancellation follow-up `cd8b4eaf1` passes all six direct proof
and admission cases on both platforms. It changes tests alone. Two new
public fixtures and seven additional boundary programs match CPython
3.13.2. Refusal or cancellation during the second default admission
preserves its span and refunds before the division is evaluated.

Both images pass six script goldens. All 196 timed batches, quiet gates,
worker joins, guest fixture removal and the persistent archive verify.
The route diagnostic's initial worker request failed before verification.
The corrected procedure and original failure are retained.

[Timings](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Route counts](route-counts.json) · [JIT observations](jit-summary.json) ·
[Images](identity.json) · [Raw observations](observations.tar.xz) ·
[Procedures and patches](procedures.tar.xz) · [Terminal status](terminal-status.json).
