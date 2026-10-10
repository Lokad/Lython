# Second consolidated improvement milestone

The current runtime's **16,384-iteration integer loop qualifies against CPython**.
Its three independent sessions take **1.613–1.666 ms**, versus **0.576–0.636 ms**
for CPython. The paired estimates put Lython at about **2.6–2.8 times CPython's
time** on this job. Keyword calls, stable sorting, JSON transformation and the
zlib application also qualify. All five favor CPython; these results identify
remaining work rather than establish an overall interpreter speed ratio.

This milestone consolidates execution-thread reuse, instruction transfer, string
registration, deferred split ownership, registry/CSV accounting and list
registration since the first milestone. It adds no production optimization.
The reference and candidate lanes completed in **6m00s and 6m17s**, **12m17s total**,
with one attempt each and a 600-second whole-process-group cap per lane.
Ordinary improvement rounds continue to use seconds-long microbenchmarks with
30-second collection/cleanup caps. This full collection is an infrequent milestone.

The reference's tiny control fails because its second CPython session has
**11.15% IQR/median**, above the unchanged 10% limit. Every reference workload
therefore remains unqualified. **No job qualifies on both runtime producers**,
so this collection cannot certify an old/new improvement ratio. The historical
first milestone remains unchanged; the reference here preserves its candidate
runtime, with only the corrected preparation clock and its tests backported.

Absolute session medians retain useful diagnostic evidence:

| Workload and producer | Lython session medians, ms | CPython session medians, ms | Whole-case status |
| --- | --- | --- | --- |
| Large integer loop, reference | 2.309 / 2.268 / 2.557 | 0.632 / 0.666 / 0.714 | Unqualified (control) |
| Large integer loop, candidate | 1.666 / 1.613 / 1.659 | 0.636 / 0.576 / 0.596 | Qualified |
| ASCII pipeline, reference | 1.263 / 1.473 / 1.355 | 0.044 / 0.047 / 0.044 | Unqualified |
| ASCII pipeline, candidate | 0.141 / 0.138 / 0.140 | 0.041 / 0.041 / 0.041 | Below invocation floor |
| Retained CSV, reference | 1.063 / 0.901 / 0.985 | 0.110 / 0.099 / 0.104 | Unqualified |
| Retained CSV, candidate | 0.511 / 0.495 / 0.465 | 0.099 / 0.099 / 0.100 | Spread/order exclusions |

The candidate pipeline passes its session checks but falls below ten times the
Lython invocation control in every session. Its reduction in absolute time
supports the earlier short-run observations, while the whole job remains excluded
under the frozen execution floor. Retained CSV fails spread/order checks, with
Lython IQR/median of 11.9–14.1%. This does not explain the original intermittent
CSV failure or prove the cause of the earlier instruction-change pipeline variation.

Qualified candidate ratios mean **CPython time / Lython time**. Each interval is
a fixed-seed paired-bootstrap 95% interval within that independent session:

| Qualified candidate workload | Session 1 | Session 2 | Session 3 |
| --- | --- | --- | --- |
| Large integer loop | 0.383 [0.381, 0.387] | 0.357 [0.355, 0.359] | 0.359 [0.356, 0.361] |
| Keyword calls | 0.158 [0.156, 0.159] | 0.166 [0.165, 0.167] | 0.163 [0.161, 0.164] |
| Stable sort | 0.071 [0.068, 0.072] | 0.070 [0.065, 0.071] | 0.070 [0.065, 0.072] |
| JSON transform/roundtrip | 0.145 [0.143, 0.148] | 0.150 [0.143, 0.161] | 0.158 [0.157, 0.161] |
| Zlib application | 0.115 [0.114, 0.116] | 0.114 [0.114, 0.115] | 0.115 [0.114, 0.116] |

The complete generated [reference](reference-warm.md) and
[candidate](candidate-warm.md) tables retain every case, session and exclusion.
Reference stable-sort and tuple-key updates stopped after an excluded session;
candidate tuple-key updates did too. Other exclusions include incomplete timing
evidence, spread/order/interval/session checks and invocation floors. None is
recollected or relabelled. No pooled interval, control subtraction or memory ratio
is reported.

Both producers use the same fourteen predeclared canonical quick-profile cases,
Python source, fixtures, full goldens and v6 harness/eligibility. Each engine runs
precompiled source in fresh execution state, with complete output comparisons
inside timing; framing and hash encoding remain outside it. Lython keeps the
ordinary public options, containment, output capture and projection costs.
CPython uses isolated `-I -S`, normal GC/GIL and no equivalent in-process governor.
These are public invocation comparisons, including complete library applications,
and do not isolate the dispatch cost or prove that containment explains the gap.

Reference producer: `f90734ec1444c15f0815af0a232c49d71320f24f`, preserving first
milestone runtime `4fb489d97783aab6cadc94387661f60378f689c5` and its production
`src` tree `22668d84b6209a539ae6bf0be3d4e56d361042c9`.
Candidate producer: `39952af6e4c645c0733e6eda771cc513ecea16f0`, matching delivery
`dff745da7ede15fbeabb5a1fbef88c258f387ccc`, production `src` tree
`d49b77eada4a8f79d2f9f7287314c380f342147c`.
Both benchmark trees are `c194ce88a7e4d52c576f3929ef82bce60bf37e2d`; their
catalog SHA-256 is `e6cf197948d903c59d4db5e940d9ca50433cbb0c6399ab999817f48775fa4fe1`.
The frozen declaration precedes collection at **2026-10-10 04:15 UTC**.

Both clean Release producers use SDK 10.0.401/runtime 10.0.12 and optimized
CPython 3.13.16. Each passed **373 comparison-related public checks** on the VM
before collection. Both source heads passed Windows/Ubuntu CI, including probes,
full Release suites and package consumers:
[reference CI](https://github.com/Lokad/Lython/actions/runs/38023159287),
[candidate equivalent delivery CI](https://github.com/Lokad/Lython/actions/runs/38022215477).

The offline audit verifies **1,103 reference and 1,203 candidate successful
responses**, complete goldens, invocation counts, identities and timing formulas.
It independently recomputes **78 session statistics**, including medians,
IQR, pair-order effects and the specified 10,000-draw bootstrap; those agree
with the frozen verifier. It checks qualified-session preparation, warmup,
calibration, measured floors, noise counters, controls and independent processes.
Every timed input was physically rehashed afterward. Both collection services
record success, the actual hard caps and zero main PIDs; final checks prove
inactive/unloaded services, absent recorded workers, clean roots and a free lease.
Builds/tests/transfers finish before timing; rendering and audits follow collection.

Both controllers printed a redundant stop error after their already-stopped units
had unloaded. Their collection exit codes are zero and their terminal evidence
was preserved before that error. Reference cleanup was verified before starting
the previously unrun candidate. Neither producer was rerun, and the original
collection scripts and declaration remain hash-identical.

| Artifact | SHA-256 |
| --- | --- |
| Reference receipt | `4afc066710769013a4246ae8d24ae402668369ab846cb7ca24f292760dfc5e3a` |
| Candidate receipt | `be292b127cd0e902a08f7288c7c5bbe3ba4f45c5a02cd730cad8bf37fd552035` |
| Reference rendered report | `6f8660a98d4ebd76f351423aeab099d3dceba6e7ea55ae4f05a5090545d3d4c4` |
| Candidate rendered report | `5b9e069fb393dbf6d105762572c538c3a548b26c769bceff419c0dc40f06707f` |
| Independent audit.py | `5bf4002944302d1f8506b4654fc22ef62a4f9541dc06e6ab89e9ebd8e7f7d077` |

[Evidence](evidence.json) retains the frozen declaration, eligibility, independent
statistics and cleanup/input proof. Raw receipts, every attempt, scripts,
correctness logs and original reports remain under ignored
`.git/agent-notes/second-milestone-20261010/`. Report files use LF endings so their
committed hashes match the original rendered bytes. Matched old/new and secondary
lane qualification remain pending a future declared milestone. Further routine
runtime investigations use short diagnostics and microbenchmarks.
