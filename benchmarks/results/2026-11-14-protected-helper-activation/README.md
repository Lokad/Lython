# Protected helper activation

R368/B756, 2026-11-14 UTC. Retain frozen `b5a6426e` against committed
`1c5af539` / `ff2b5d76`, pending full delivery qualification. Core source and exact
binary are archived; all582inputs are checked. Same Python source/data across
runtime roles, unlike [R367's source-form diagnostic](../2026-11-14-protected-helper-cost/README.md).

Remove the blanket native exclusion for try/except and its eager successful-call
context/backing allocation. Existing final slots execute the normal body; skip
handler-only instructions in eligibility. Actual catchable faults retain their
exact PC and transfer live materialized values into existing injected-exception
routing. No operation replay or second handler dispatcher. Finally/with/generators
keep their original route. Three production changes plus a routing comment; no
new public API, policy, pool, memory mechanism or dependency.

Quiet Ubuntu24.04.4/F4as_v7/four EPYC9V45/.NET11RC3, normal GC/tiering, fixed
workers/adapter0.4. Eight jobs304.418s, four fresh processes/job, five-second
full-request warmups/twelve for text, five rotating half-second batches.

| Complete script /phase | Elapsed change | Wider CPU change |
| --- | ---: | ---: |
| Protected 4,096-record invoice /warm | −18.50..−17.09% | −17.91..−16.67% |
| Same /compile-run | −18.95..−15.70% | −18.07..−16.13% |
| Protected 2,049-line early-exit search /warm | −42.77..−40.76% | −38.41..−36.72% |
| Original invoice /warm control | −1.23..−0.52% | −2.37..0.00% |
| Text /warm control | −0.30..+3.72% | −1.96..+3.32% |
| JSON /warm control | −2.04..−0.55% | −2.07..−1.38% |
| Short helper /warm control | +0.48..+3.38% | +0.74..+2.75% |
| Protected invoice /compile | −1.90..−0.44% | −2.83..+0.45% |

Negative means lower/faster; all independent process-median pairings, not
confidence intervals. Text reference processes spread2.75%, short2.50%; keep
text uncertainty and accepted short debt. No neutrality or causal layout claim.
Compile CPU is mixed. Full sources, warmups, round observations, IQR and controls
remain in the raw and summary. Fixed percentages are not retention vetoes.

Warm includes mediated RAM I/O, stdout/file validation and prior-result cleanup;
reset/protocol/startup/compilation/final cache teardown excluded. Fresh adds
per-invocation compilation/disposal. Compile excludes guest execution. Wider CPU
also includes reset/protocol. These are Lython source-pair results, not CPython
ratios, startup or embedding-latency measurements. B747 remains at earlier fa40/73a.

Separate local allocation diagnostics reuse exact R367 ff2/tool/catalog baseline:
protected invoice7,408,429→5,245,694B(−29.19%); search582,077.75→147,554.875B
(−74.65%). Unprotected invoice+0.0033%,search+0.1012%. Includes reset/mediated
I/O/full validation/result cleanup, excludes compile/startup/final cache teardown.
No forced GC. Allocation and demonstrated CPU benefits are separate evidence.

Focused actual11/10 each pass181internal+306public; after four additional semantic
cases, final16protected checks pass on both targets. All124isolatedCPython controls
and16source/phase goldens match. Coverage includes live defaults, nonlocal effects,
exact fault PC/else range, nested child faults, handler headers/deletion/context,
owned-wide integer storage, finally behavior, real handler I/O, retained results,
cancellation/join and terminal refusal. Source/binary stays unchanged through the
test supplement. Initial fixture never assigned its handler variable outside the
except path; unchanged static analysis rejects it with LA3146. The corrected
fixture initializes it first and still tests deletion. Original failed logs,
diagnostic/control comparison and initial compilation corrections are preserved.

Retain this bounded migration with explicit short debt and text uncertainty.
Full platform/original stress/installed-package delivery follows before claiming
qualification. Fault promotion can require admission; identical tiny thresholds
are not promised. Current managed stack/cancellation/ownership/terminal refusal
contracts remain. The whole-script objective continues; publication held.

All image/catalog/golden/file/budget/quiet/join/archive gates pass. Raw bytes verify
under the lease before teardown; no users/fixtures remain. SHA256
`3c8368cd0ffd3db0082b9a3148ac4bf2f7a78aeead981ccf525d5c0f2404022b`.
Free space267,853,824B;254/252MiBfloors/current images/catalog/toolchain unchanged.

[Decision](decision.json), [all timing](timing-summary.json),
[allocation](allocation-summary.json), [raw](observations.tgz),
[exact source/binary/local checks/failures](source-and-local-checks.tar.xz).
