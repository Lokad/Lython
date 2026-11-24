# Current annotated factory call attribution

Windows and .NET 11 RC3 profiles use the source-matched `4eb8c9559`
image from production `5313f9e71`. Four CPython 3.13.2 goldens and all
32 worker image files verify. Windows EventPipe samples the warm request
for approximately six seconds after about four seconds of warmup per
script. Sampled CPU intervals require a resolved runtime ancestor. Wait
and unmanaged intervals sit outside the table. This is local attribution.

| Script | Resolved runtime ancestry | Dispatch | Located admission and cleanup | Unresolved under native entry |
| --- | ---: | ---: | ---: | ---: |
| Eager annotated factory | 5,919 ms | 1,206 ms | 688 ms | 0 ms |
| Postponed annotated factories | 5,887 ms | 1,385 ms | 904 ms | 0 ms |
| Captured rules | 5,960 ms | 7 ms | 4 ms | 5,734 ms |
| Defaulted rules | 5,951 ms | 23 ms | 14 ms | 5,645 ms |

The captured and defaulted stacks usually stop at `StartNativeCall`.
That boundary contains the body, so these traces leave the prior quiet
attribution as the useful breakdown for those scripts. In the annotated
traces, reserve callers resolve to eager annotations and defaults for the
eager case and callable creation and defaults for the postponed case.
These intervals overlap other inclusive costs and predict no speedup.

Exact tapes establish the candidate scope independently of sampling.
The defaulted factory has 23 instructions, including two complete
lexical definitions and a prepared tuple return for their private local
bindings. Eager and postponed default factories already take the direct
route. The captured factory has an actual nested `alternate()` call.
Its distinct closure and live captures need their ordinary semantics.
The pinned Windows FullOpts listing with tiering disabled places a
440-byte frame at `StartNativeCall` and a 216-byte frame at `StartInline`.
It confirms the default-initialized workspace. Compilation capacities are
seven slots for the defaulted factory and ten for the captured factory.
The stack sizes include compiler-generated spills and call state.

A further source candidate should remove a complete creation and return
sequence from the existing workspace, preserving fresh identities and
resumable execution for ineligible cases. The defaulted factory offers a
bounded proof shape. Compare all rule variants and the text and invoice
controls and identify the old machinery that the new path retires.

[Attribution](attribution.json) · [Factory tapes](factory-shapes.json) ·
[JIT observations](jit-summary.json) · [Capture summary](run-summary.json) ·
[Raw traces and procedures](observations.tar.xz) ·
[Measured core](../2026-11-24-decoded-closed-defaults/identity.json).
