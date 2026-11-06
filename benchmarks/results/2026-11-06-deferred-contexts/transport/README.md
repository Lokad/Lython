# R13 publication transport completion — 2026-11-06

Remove the duplicate defining-scope parameter from shared result publication and
the NoInlining fused-store completion. Its sole use was cancellation; owned operand
storage already retains the identical ExecutionState. Lexical stores still receive
their actual invocation. Retirement/disposal order, first refusal, native returns,
real suspension and the liveness fence remain unchanged. No new policy or owner.

The representative text, JSON and report helpers already use NativeReturn;
always-promoting helper setup cannot explain these controls. That source hypothesis
was deferred without adding compiler facts, a pass or another dispatch mechanism.

B348 compares the five-file patch after `abe0defb` (core `977da8d6`) with committed
R13 B `9a152ee0` (source-equivalent frozen core `1daf56c3`). The same worker,
fixtures, timing boundaries and full-output/cleanup checks are used. Five-second
warmups, seven rotating150ms rounds; identical candidate roles and CPython.

| Workload / boundary | Elapsed vs B, candidate / identical role |
| --- | ---: |
| CSV warm | −0.15% / −1.04% |
| CSV fresh | −0.01% / −0.57% |
| Text warm | −1.93% / −3.64% |
| JSON warm | −1.14% / −2.23% |
| Report warm | +2.63% / +0.67% |

Retain the simpler transport. CSV is effectively flat/control-limited; text and
JSON are favorable, while report remains adverse/uncertain with a1.91% identical-
role split. No broad CPU/exact-allocation gain or compound debt-repayment claim.
R13's remaining regression debt and the whole-script objective stay open.

All five jobs completed in139.83s with quiet endpoints, loaded hashes, goldens,
funding, Closed, joins and fixture cleanup. Raw archive SHA256
`4292525fb4bc4ba6adfec9208f7aa83771f3942b224da6aa4d43a5565d298a9e`.
All evidence is persistent; no worker/fixture/collection remains. Both-target
Release build has zero warnings/errors; focused526internal/1,508public per actual
.NET11/.NET10 pass, including ownership, liveness, refusal, cancellation and real
I/O. Full B qualification does not qualify this patch; batch its platform/stress/
installed-consumer delivery with the next meaningful runtime boundary.
