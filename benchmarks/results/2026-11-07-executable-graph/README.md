# Dense executable block normalization — R58/B415

2026-11-07. Retain the complete graph-phase replacement on4885d9c0/R57;
before/current coresbd1e50f5/aa133dcb, unchanged normal0.4 adapter and worker.
The [exact source/identity](identity.json), [pre-code plan](pre-code-plan.md)
and original [inconclusive design](../2026-11-02-compiler-normalization-unqualified/README.md)
remain. Earlier2.83–3.77%CSV/0.81–2.11%text compile observations had69–133%IQR
and10.99%fresh-control drift; rigid cutoffs were not evidence against the design.
Current source still repeated per-target walks, justifying bounded confirmation.

One iterative all-target table resolves ordinary jump chains, including cycles.
Dense reachability/remapping replaces per-query HashSets, a reachable HashSet,
sorting and dictionary lookup; normalized instruction arrays are filled directly.
Cycle nodes keep their own identity and tails their first cycle entry. Protected
AbruptJump keeps its original target for iterator/exception cleanup. Existing
region scans and binary searches remain; only jump-chain resolution is linear,
not every operation in the entire compiler.54net production lines are added for
the shared algorithm while those collection mechanisms and repeated walks disappear.
No extra execution route, optimizer framework, public option/cache or value format.
Compiler defects fail explicitly. Existing coarse compilation funding, managed
depth, stage cancellation and terminal refusal remain. Later compiler passes can
still finish before the next token check; no new in-flight cancellation claim.

**429internal/5,143public focused Release checks per actual.NET11/10 PASS.**
Both matching probes are built and their core bytes match. The reference oracle
checks all partial successor graphs through five nodes, distinct cycle entries,
incoming tails, a20,000-node chain and invalid internal successors. Public loop,
match, generator, exception, scope, cancellation/refusal and product checks pass.
Initial internal350checks also pass. One overly broad initial public filter
accidentally selected Stress: the owned process tree was stopped and the exact
selection/log preserved. The successful run explicitly excludes Stress. No test,
timeout, assertion or original stress fixture was weakened; full delivery retains
those original checks separately.

## Controlled current-source comparison

Quiet F4as_v7/Ubuntu24.04.4/.NET11RC3, ordinary GC/tiering, three independent
reference/reference-control/candidate processes.5s warmup per role/job,
11rotated~200ms rounds. Five jobs complete in124.997s; full goldens, exact source/
auxiliary/actualReady, quiet endpoints, Closed/exit0/join/emptyfixtures pass.
Persistent records are separate from an owned128MiB fixture tmpfs, archived and
verified under the lease and locally; fixtures unmounted/removed.180sowner+
30sstop+30smargin; service is terminal. Never requery B415.

Positive percentages mean current slower. These are source-pair observations;
CPython is not run, and the primary comparison table remains frozen55e510ff.

| Boundary / complete script | Current | Change vs both unchanged roles | Old-role spread | Current IQR |
| --- | ---: | ---: | ---: | ---: |
| Compile CSV helpers |67.47us|−1.66/−2.97%|+1.35%|0.43%|
| Compile text helpers |48.57us|−2.20/−2.90%|+0.72%|1.65%|
| Compile JSON-to-Markdown |45.41us|−1.76/−2.60%|+0.86%|0.92%|
| Compile+run config |147.71us|−1.61/−0.62%|−0.99%|0.91%|
| Warm text control |1.9178ms|−2.84/+1.49%|−4.26%|0.52%|

Compile includes construction/disposal; fresh includes compilation, mediated RAM
I/O, full validation and prior-result cleanup. Warm excludes compilation. All
exclude startup, fixture staging/reset, transport and final cached teardown.
External process CPU is10ms-quantized, includes work outside internal job clocks
and is mixed/adverse (text compile+1.14–2.41%, fresh config+1.36–2.67%).
**No CPU improvement or warm-execution gain is established.** The warm identical
roles differ4.26% despite narrow within-role IQR; preserve process variation and
the+1.49%adverse point as uncertainty/debt, not proof of zero runtime impact.
No unchanged immediate retiming campaign; revisit at a coherent representative
checkpoint. No fixed improvement percentage veto or general AST/backend rewrite.

Separate existing five-script desktop compile diagnostic, unchanged normal driver:
allocation−5.89%search,−4.78%text,−4.05%CSV,−3.89%JSON,−4.19%report.
Goldens/isolation/funding/cleanup pass. Elapsed mixed/adverse, report old-role
spread41.76%; raw clocks remain. It does not establish CPU benefit. Retain for
eliminated repeated work, lower allocation and modest controlled compilation
elapsed improvement. This does not close the substantial whole-script objective.

Full combined R57/R58 delivery CI42404834827 on e66b8f72 is GREEN: Windows/Ubuntu,
actual11/10, original Stress and installed consumers. [Cached terminal receipt](delivery-ci.json);
never query again or repeat unchanged qualification.
Publication is postponed. [Controlled assessment](assessment.json),
[local assessment](local-assessment.json), [focused counts](final-focused.json),
[VM sources/cores/raw samples](observations.tgz), [receipt](archive-receipt.json),
[local sources/tests/failures/raw samples](local-observations.tgz).
