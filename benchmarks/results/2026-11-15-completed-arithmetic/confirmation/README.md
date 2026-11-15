# Same-bits controls and retention decision

B791, 2026-11-15: unchanged **9e890d18** versus **22e5b72c**, two
processes/source, five rotated 0.5 s batches. Full-request warmup increases to
12 s for captured text and 8 s for the existing six-helper configuration.
Two jobs finish in 112.245 s; all golden, identity, ownership, quiet, joined
cleanup and archive checks pass. Terminal cached; no users or fixtures remain.

| Complete control | Elapsed change | Wider process CPU change |
| --- | ---: | ---: |
| Captured text / warm | −1.30..+0.81% | −0.25..+1.00% |
| Six-helper configuration / compile-to-result | −0.28..+0.33% | +0.35..+3.08% |

All process-median pairings remain. Text reference/candidate elapsed spreads
are 2.134/0.002%; fresh spreads 0.193/0.421%. The original B790 adverse fresh
process and moving controls stay [preserved](../README.md). Longer warmup is
a justified measurement correction, not proof of the original cause or neutrality.
The fresh wider-CPU increase remains accepted modest regression uncertainty.

**Retain the shared completed-arithmetic/operand boundary:** it removes duplicate
selection and operand reading, keeps the existing lean native path, and B790
shows stable invoice elapsed 2.29–3.84%/CPU 2.14–3.30% lower. Allocation is flat;
the source pair also contains the live-default correctness correction. No isolated
causal claim, universal arithmetic fast path or substantial-objective completion.
Full platform, original stress and installed-package qualification is pending.

Boundary, VM and runtime settings are unchanged from the original collection.
Warm includes Run, mediated RAM I/O, goldens and prior-result cleanup; fresh
also includes compilation and compiled-owner disposal. Wider CPU includes
reset/protocol. No new CPython/startup/pending-I/O/heap/tail claim.

[All controls](summary.json), [decision](decision.json),
[original observations](observations.tgz), [terminal state](terminal-status.json),
[exact procedures](procedure.tar.xz).
