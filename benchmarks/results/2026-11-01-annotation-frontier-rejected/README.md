# Annotation-frontier trial: rejected, removed (B57)

2026-11-01. **Prototype removed.** CSV compilation improves5.53%/4.85% against
its originals, missing the frozen5% against both. Warm CSV original controls
differ3.55%, exceeding3%. No accepted speedup, retiming or threshold change.
Seventeen independent public regressions remain.

## Proposed deletion of unused analysis

[Plan before code](pre-code-plan.md): stop annotation dataflow after its last
possible diagnostic at module/function/class scope boundaries. Keep the complete
binding prefix and all branch/loop/try/with/match effects before later checks.
Retain existing mismatch rules/order, scope resets and the real static analyzer.
A syntax-only frontier scan replaces unused trailing abstract interpretation;
current compile cancellation and admission remain active. No cache, framework,
public option or owner redesign. [Exact patch](source.patch.gz).

B45's saved compiler chains include43/613 with AnnotationDiagnostics.AnalyzeStatement;
that is incomplete inclusive attribution, not a removable fraction. Both maintained
helper scripts have annotated functions followed by unannotated processing. The
hypothesis concerns compilation; warm execution is an independent control.

## One comparison and all observations

Original production95b5955c frozen at20e208c0; candidateaf3491b3 +patch. Fresh
nonincremental build, equal core/worker/test/probe copies, unchanged auxiliary
hosts/adapters. [Identities](candidate-identity.json). Dedicated Ubuntu24.04.4
Azure F4as_v7, four EPYC9V45 vCPUs/16GiB, .NET11RC3. Private existing-driver
adaptation loads original/candidate/identical-original images sharing CoreLib
and actual key hashes; ordinary GC/tiering, no forced collection or unload claim.

| Phase / complete job | Before ms | Candidate ms | Original control ms | Candidate vs both |
| --- | ---: | ---: | ---: | --- |
| Fresh compile CSV helpers | 0.087170 | 0.082350 | 0.086549 | -5.53% /-4.85% |
| Fresh compile text helpers | 0.068365 | 0.063985 | 0.067948 | -6.41% /-5.83% |
| Warm CSV, unaffected | 8.22264 | 8.26288 | 8.51466 | +0.49% /-2.96% |

Compilation includes fresh public Compile, validity and script-owner disposal;
a resident output-control script is unchanged. No script execution or startup
inside compilation timing. Warm includes fixture reset, mediated real I/O, full
goldens and result disposal. Do not subtract phase medians or infer CPython ratios.

Each compile series uses1024 interleaved warmups/image and seven rotating4096-job
batches; warm uses128 warmups and32-job batches,2s settle/case. Every batch>100ms,
relative IQR0.28–2.37%. CSV/text compilation controls-0.71%/-0.61%; warm+3.55%.
All samples/outliers stay visible. Low dispersion does not repair failed gates.
All quiet/hash/output/balance/fixture-cleanup checks pass, service exits0 in36.678s
within45s. Inactive/dead/success, MainPID0, empty group; exclusive lease free.
Coexisting images change shared process GC/JIT pressure; this is private diagnostic
evidence, not general repeatability or qualified embedding latency.

## Semantics, failures and restoration

Candidate zero-warning Release build;196 native/749 public focused checks per
actual.NET11RC3/10.0.8 pass. Ten new mismatch controls preserve branch effects,
nested defaults/returns/classes and unreachable diagnostics. Seven runnable
loop/scope/annotation-free cases match isolated CPython3.13.2, run through both
public APIs and return shared allowance to baseline/zero after cleanup.
New17 controls also pass against frozen original.NET11 image in an isolated copy
of the current public test output. No tests require the removed internal design.

Two initial fixture expectations incorrectly demanded errors after conservative
try/match joins; corrected to preserve established behavior and retain actual
nested/finally mismatch cases. An initial private-driver build lost tuple element
names through Append; corrected before freezing/collection. Logs preserve both;
no production fix or performance retry is attributed to them.

Original analyzer restored with fresh timestamps and nonincremental build;
production diff empty, equal fresh core/worker/test/probe copies. Same196/749
focused checks pass per actual target, including17 new controls. [Restored identity](restored-identity.json).
Existing terminal production qualification95b5955c/41296168049 is not repeated;
new tests' platform qualification is batched at the next retained delivery.

[All observations/gates](summary.json), [semantic goldens](semantic-goldens.json),
[validation logs](validation-logs.tar.gz), readable [driver](Program.cs)/[collector](collect.sh)
and [raw records](raw-results.tar.gz), SHA256
`f1ce8d6e8c3eab15e4770c3e936fb8a873a4ee540a9b5f33f831da5c1cc7d6f4`,
preserve the decision. Stop this prototype. Do not tune its syntax scan, change
its acceptance gates or retain a smaller fragment as an inferred speedup.
