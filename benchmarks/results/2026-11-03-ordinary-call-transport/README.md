# B165 complete ordinary-call transport

2026-11-03. Runtime source **d3261a39937bc95595d5a24eca51c1b1bd4a770d** retained;
fixture correction **767250651ad4079d06dc998437db4a242a683592** changes tests only.
Publication postponed. Full corrected delivery41651037909 is pending; source is
focused-qualified and measured, not yet fully platform/package-qualified.

The Call opcode reads its evaluated target once, offering it to the existing
linear frame runner. Remaining calls share one non-async native/general selector
and completed stack transfer. The duplicated synchronous ExecuteExecutableCall,
async value-operation Call body, second target read and orphan dispatch arms are
deleted (net77 production lines removed). Native results retain object transport;
only potentially asynchronous branches use the completion helper. Actual pending
calls keep their operands and general argument reservations/packets through awaited
completion and cleanup. GetIter keeps its cursor operation with a completed prefix.
No new binder, opcode catalogue, strategy framework, owner layer, cache, pool,
public option or memory/cancellation policy. No intended compatibility change.

## Four-image whole-script comparison

Same process/CoreLib/hash, ordinary GC/tiering, exact auxiliary images and actual
RAM-backed file host. All images use the same selected API. Before/unchanged-control
are B162/b087, candidate B165, extra pre-shared-driver image B160/873. Thus the
B162 debt is assessed against the earlier runtime in the SAME observations, not by
subtracting independent historical deltas. Four complete CSV/text/JSON/report
scripts;512 warmups/image and nine rotated32-job batches. The complete timer includes
fixture reset, public invocation, actual mediated I/O, full stdout/file checks and
result disposal; compilation/startup excluded. Nested Run-only values remain raw.

| API/workload | B162 ms | B165 ms | Elapsed change /unchanged B162 control | B165 versus pre-driver elapsed /CPU |
| --- | ---: | ---: | ---: | ---: |
|sync/csv-summary-helpers|5.4231|5.3358|-1.61% /-2.09%|-0.43% /-0.24%|
|sync/text-summary-helpers|3.6545|3.6410|-0.37% /+0.99%|-0.73% /-0.81%|
|sync/json-summary-helpers|5.9395|5.6874|-4.25% /-1.55%|+0.44% /+0.47%|
|sync/report-render|6.0672|6.0920|+0.41% /+0.74%|-0.20% /-0.10%|
|async/csv-summary-helpers|6.2895|6.0672|-3.53% /-1.13%|-6.71% /-6.53%|
|async/text-summary-helpers|4.1529|3.9181|-5.65% /-0.35%|-14.37% /-14.19%|
|async/json-summary-helpers|6.3838|6.2367|-2.30% /+1.98%|-8.66% /-8.59%|
|async/report-render|6.4529|6.3448|-1.68% /+1.68%|-5.09% /-4.79%|

Relative to B162, async elapsed improves1.7–5.7%; CPU agrees. Synchronous JSON
improves4.25%versus−1.54%control; other synchronous cases are near their controls.
Combined B162/B165 versus pre-driver async elapsed−6.7%CSV/−14.4%text/−8.7%JSON/
−5.1%report. This does not attribute all of that gain to the single target read,
and does not explain the external synchronous CPython gap.

**Allocation and accounted peaks are effectively unchanged** (allocation within
0.03%, comparable incidental controls). Physical RSS/embedding latency is unmeasured.
All output/file goldens, hashes/isolation, balances and before/after quiet gates
pass; every result/program owner/fixture/process joins. Owned service178.295s,
exit0, unit inactive and shared lease independently free. No abandoned work.

**Debt decision:** retain the complete architecture; close the provisional B162
synchronous blocker at this coupled milestone. Candidate versus pre-driver sync
is within−0.73/+0.44%; the earlier roughly2%adverse text/JSON result is not reproduced
here. Controls still differ up to2.1%, so this is not proof of zero regression in
all conditions or disproof of older adverse observations. Those remain immutable
in B162. No remaining material sync correction is justified by this bounded
comparison. The overall whole-script performance objective and B138 unique-key
debt remain OPEN. No extra unchanged replay or blanket percentage veto.

## Validation and original CI failure

Focused Release **5618 public/800 internal per actual.NET11/.NET10** PASS. Includes
Python defaults/keywords/bound receivers/closures/generators/rebinding/evaluation
order, native return optimizations, real async hosts/protocols, managed stack safety,
first terminal refusal, cancellation, retained aliases, actual cleanup and budget
reuse. Existing semantic/lifetime tests reused; no architecture-only tests added.

Original full run41647907097: Ubuntu.NET11/.NET10, installed consumers and original
Stress PASS; Windows.NET11 public15801PASS but direct ExactBytesSum allocation
control reports8168B versus4096B. It calls the unchanged Sum helper directly,
outside the changed call route. Original20-case local focused and4040-case full
white-box reproductions PASS. Exact cause remains unresolved; no proven GC/tiering
attribution. Original terminal failure and complete Windows log retained.

Fixture correction76725065 joins the existing nonparallel AllocationSensitive
collection and replaces one warmup with bounded eight-call warmup. Original8192-item
input and4096B bound, semantic/refusal/cancellation/balance assertions are unchanged.
Corrected20-case selection PASS on both actual runtimes. No timeout/threshold was
relaxed and no production code changed; no runtime retiming. Corrected full
41651037909 remains pending. Do not re-query terminal original41647907097.

Baselineb087 corea3aa23470c2437acf40e4d26db47bdc1b9e3bd35b62b5e0aeb3dd783fcfe54a6;
pre-driver873 corea890ccf44ea9ee7ccbae436cfc64b39bb6367191f1a9aaf42c8ef5388ac2da79;
candidatee25a735305f1dfb429c834ac00d0acac4b5179d6c2af605794be7d59250a1591,
frozen before matching source commit. Exact source hashes in identity; same quiet
Azure Standard_F4as_v7 Ubuntu24.04.4.NET11RC3 environment as B162/B163.

[Assessment](assessment.json), [raw observations](observations.tgz),
[identity](identity.json), [source patch](source.patch), [source gate](source-gate.md),
[original CI receipt](ci-original-status.json), [original Windows log](original-windows-failure.log),
[fixture-only correction](allocation-fixture.patch).
[Prior shared-driver variants/adverse controls](../2026-11-03-shared-instruction-driver/README.md)
and [latest external CPython checkpoint](../2026-11-03-shared-driver-cpython-checkpoint/README.md)
remain separate.
