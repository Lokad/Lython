# B1591 Reuse decoded integers in closed defaults

Production `5313f9e71` matches the measured source `4eb8c9559`. The
computed factory passes its already decoded argument to the shared
closed-default evaluator. Arithmetic reads machine integers directly
and uses the existing funded exact path for wide values or exceptional
operations. Captured keyword defaults reach their original storage
without a temporary slot. Eligibility, source checks and ownership are
unchanged.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 24,
2026 UTC. The 32-file frozen images differ only in the core DLL. Both
annotated factories compare the prior (`af717101d`), computed-default
reference (`d40960ae`) and decoded candidate (`4eb8c9559`) in the same
rotations. Four controls use the reference and candidate. Each role warms
for eight seconds and has two processes with seven timed batches each.
Ranges cover all pairings of worker medians. Warm elapsed includes
mediated RAM-backed I/O, verification and previous-result disposal.
Process CPU also covers reset and request handling. Compilation,
startup and final process teardown sit outside the clocks.

| Warm script, candidate vs computed-default reference | Elapsed change | Process CPU change | Elapsed spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Captured rules control | −10.50 to −0.78% | −10.43 to −0.92% | 9.96 / 0.83% |
| Defaulted rules control | −1.30 to +10.07% | −1.96 to +9.87% | 4.10 / 7.13% |
| Eager annotated rules | −4.48 to −2.88% | −4.66 to −3.01% | 1.51 / 0.16% |
| Postponed annotated rules | −0.83 to +5.63% | −0.67 to +4.88% | 0.41 / 6.08% |
| Short text control | −10.82 to +9.77% | −11.01 to +9.30% | 7.45 / 14.56% |
| Quoted invoice control | −1.71 to +1.94% | −1.96 to +2.00% | 1.77 / 1.91% |

Eager rules take 41.96–44.10% less warm time than the prior image in the
same run. Retain the removed operand conversions with the eager gain.
Postponed rules span −3.50 to +5.99% elapsed and −3.00 to +5.11% CPU
against the prior. The computed-default reference spans −3.09 to +0.75%
elapsed against that prior. These data leave the B1590 postponed regression
question open. Untargeted rule and text controls moved substantially.
Their measurements limit attribution of script-wide changes.

All three images pass six CPython goldens, both quiet gates and all
196 timed batches. Workers join, guest fixtures retire and the persistent
archive verifies. Local allocation diagnostics are flat on both affected
shapes, all six admitted peaks match and every run restores its allowance.
Windows and Ubuntu each pass 276 public and 286 white-box focused Release
checks, including arithmetic, exact error spans, alias and annotation
identity, cancellation, refusal and managed stack admission.

[Timing](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Images](identity.json) · [Reference core](../2026-11-24-closed-default-evaluation/candidate-core.dll.gz) ·
[Prior core](../2026-11-24-eager-json-owner/candidate-core.dll.gz) ·
[Observations](observations.tar.xz) · [Procedures and patch](procedures.tar.xz) ·
[Terminal status](terminal-status.json).
