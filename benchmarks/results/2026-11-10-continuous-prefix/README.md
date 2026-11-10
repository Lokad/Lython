# R194 — continuous prefix exception boundary

2026-11-10. Qualified runtime1a83c64f/frozenac6 reference; originale96/refined8f
prototypes abovef3e11cc1 were isolated and are **not promoted**. Main runtime stays
unchanged. This closes these variants, not the whole-script performance objective
or all future dispatch designs.

The same opcode engine ran continuously inside one exception boundary, moving
only real failure routing out of the hot run. Per-instruction current-token/first
admission checks remained; no fuel, owner, arena, engine or unsafe code was added.
Original and refined focused checks pass3221public/1208internal on each actual
.NET11/10. No full platform/package qualification was selected for withdrawn code.

Windows JIT proof shows the jumped stack-home removed, but OSR code grows from
8932B to10313B(original)/10099B(refined); original observed sub-rsp grows512→800
(not total stack usage). Allocation remains effectively flat. Removing a source
boundary did not establish less runtime work. Both sources, cores, complete logs,
JIT bytes and desktop adverse measurements survive in
[prototype evidence](prototype-evidence.tar.xz), indexed by SHA in
[prototype index](prototype-index.json).

B542 reuses the unchanged short complete-job driver on quiet F4as_v7 Ubuntu24.04.4,
.NET11RC3/stockGC/tiering. Four fresh processes, two per public API; each compares
two identical references and one candidate, with32warmups/role and5rotated rounds
of32invocations across the same five complete scripts. See [summary](summary.json)
and [raw archive](observations.tgz). All goldens, balances, image identities, quiet
gates, worker joins, RAM cleanup and local archive SHA checks pass. Terminal status
is cached; no VM users remain. All rounds and original failed conditions survive.

| Workload | Sync Run change | Async Run change | Interpretation |
| --- | ---: | ---: | --- |
| Bound-method text | +8.38..+15.92% | −8.24..+22.03% | Sync adverse; async variable/adverse |
| CSV | −1.64..+0.80% | +2.62..+3.99% | Small mixed sync, adverse async |
| Ordinary text | −2.20..+3.25% | −0.94..+6.81% | Uncertain/adverse |
| Report | −1.90..+3.29% | −5.28..+0.60% | Earlier async regression not reproduced consistently |
| Search | −59.86..−2.14% | −85.02..+8.53% | Unusable CPU/gain evidence: identical controls drift55–84% |

Ranges are candidate versus both reference process medians, not confidence
intervals. Early rounds contain pronounced tier/JIT activity (including background
CPU above elapsed), and several unchanged controls move. Quiet-machine gates do
not imply steady-state tiering. Do not infer search speedups, isolated guest CPU,
or general failure/success from these values. Allocation is flat, with identical
admitted peaks. The original desktop async report+7.27..+15.69% and search
+3.19..+6.65% remain explicit in the archive, unexplained rather than erased.

Boundary correction: driver executionMilliseconds spans only Run/awaitedRunAsync,
including mediated I/O and projection; fixture reset, goldens and result disposal
sit outside that clock. elapsedMilliseconds includes them. Startup/compilation
are excluded from warm samples. [Summary](summary.json) reports both clocks and
wider shared-process CPU/allocation. The async host completes I/O synchronously;
real suspension is covered by correctness tests, not this timing. Older driver
purpose/boundary labels are retained verbatim; this correction takes precedence.
No primary CPython table is replaced by this diagnostic.

Decision: withdraw both variants. Neither delivers a coherent demonstrated gain
to offset code/frame growth and affected whole-script regression risk; no useful
storage disappears. This is an evidence/debt decision, not a percentage veto or
permanent one-attempt rule. Further confirmation is justified only with a changed
design or corrected steady-tier measurement sufficient to address these specific
conditions. No new benchmark framework or repeated unchanged qualification.

Space recovery retired exact canonical terminal copies and all6historicale364
hardlink aliases only after SHA/archive checks, preserving currentqualifiedac6.
The full receipt is in the raw archive. A local early observation failed its
not-before precondition before SSH; no remote probe or run restart occurred.
Publication stays postponed; independent regex and larger design work continue.
