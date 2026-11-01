# Rejected call-control record redesign (B61)

2026-11-01. **Rejected and removed.** Complete CSV and text processing regress
against both original controls, missing the frozen7.5% improvement requirement.
Production remains delivered35d8942c. No allocation saving, throughput improvement,
causal fraction or CPython ratio is claimed. No tests were added, deleted or relaxed.

## Hypothesis and source

B58's same-bits source experiment showed a material16–18% CSV/13–14% text helper
call-shape effect. This trial replaces per-call child instruction-driver objects
and their _caller chain with one driver and an admitted array of saved caller
control records. Lexical contexts and bound locals stay stable and distinct.
Invocation views keep actual operands, instruction positions, member caches and
handler-depth arrays; saved records preserve pending operations, return, exception
and generator control. Root/native-callback/generator entries retain separate
drivers. No pool, combined locals/operand buffer, universal value representation,
execution backend, owner framework or new public option.

Record capacity uses existing governor admission:512 bytes/record plus metadata,
including full replacement overlap; popped records are cleared. Views admit160
bytes, and generator estimates include their retained view. Ordinary cleanup
releases capacity after unwind; generators fund it through suspension/completion.
These are conservative accounting estimates, not measured CLR object sizes.
Earlier rejection is an intentional proposed tradeoff. Cancellation and first
refused admission remain terminal; actual async users and cleanup stay awaited.

[Initial plan before code](pre-code-plan.md), [pre-measurement criteria](pre-measurement-plan.md),
[exact candidate patch](source.patch.gz), [fresh binary identities](candidate-identity.json).
The first build had one private-access error. Initial native tests exposed21
operand-inspection failures and then six cache-inspection failures. Before timing,
actual operands/program counters/caches were kept in the stable invocation view;
the independent live-input, tail-clearing and receiver-cache assertions remained
unchanged. Two C# ref/await expressions were split into awaited results and final
stores. These corrections and every initial failure are retained in the logs.
No speed-directed tuning or threshold change followed collection.

Final candidate focused Release checks pass849 native/4,073 public cases per
actual.NET11RC3/10.0.8. Coverage includes direct/named/bound calls, defaults,
locals/closures/nonlocals, recursion/tracebacks, generator send/throw/delegation,
real protocol/host suspension, retained aliases, cancellation, refusal before
effects, awaited cleanup and shared-budget reuse. Full platform/stress/package
qualification was not run for a rejected prototype. Original source is restored
byte-for-byte and fresh nonincremental copies agree; the same focused checks pass
on the restored runtime. Delivered35d8942c's full Windows/Ubuntu ordinary/original
stress/installed-package checkpoint41319281716 is already green and cached;
no historical CI run was queried again.

## One bounded complete-job comparison

One36-second UTC window within60s on dedicated Ubuntu24.04.4 Azure F4as_v7,
four EPYC9V45 vCPUs/16GiB, .NET11RC3. Original35d8942c corea067c8a3; candidate
35d8942c plus patch, core46f4ef1e. Private unchanged host/adapter auxiliaries,
shared CoreLib and actual hash witnesses; ordinary GC/tiering. All are non-regex
jobs.128 interleaved warmups/image,2s settle/case, seven rotating32-job batches.

Warm public Run includes fixture reset, mediated I/O, full stdout/file goldens,
result disposal and ownership checks. Startup/compilation are excluded; no phase
subtraction. [Exact maintained sources/fixtures](catalog.json) are unchanged.
CSV has4,096 records, text3,072 lines; JSON uses the maintained large scale1,024
without guest helpers. All samples/outliers are retained.

| Complete job | Original ms | Candidate ms | Original control ms | Candidate vs both | Status |
| --- | ---: | ---: | ---: | --- | --- |
| CSV helpers | 8.31702 | 8.52277 | 8.21159 | +2.47% /+3.79% | Valid controls; regression |
| Text helpers | 6.49228 | 6.72691 | 6.58731 | +3.61% /+2.12% | Valid controls; regression |
| JSON control | 7.67401 | 7.66148 | 7.47670 | -0.16% /+2.47% | Inconclusive; control IQR62.43% |

Affected controls drift-1.27%/+1.46%, within3%; IQR1.31–2.78%, batches>=206.30ms.
JSON's control dispersion independently fails the prospective10% IQR gate.
Both affected jobs already fail the fixed gain requirement with valid controls;
no interpretation of JSON can rescue the trial. Metadata/indirection and saved-
record copies changed together: these data do not identify one as the cause, or
prove all frame allocation is unimportant. No favorable retiming or local follow-up.

Quiet/hash/golden/balance/owned-cleanup gates pass. Service exits0, inactive/dead/
success, MainPID0/empty group and lease free. [All observations/gates](summary.json),
[driver](Program.cs), [collector](collect.sh), [logs](validation-logs.tar.gz) and
[raw results](raw-results.tar.gz), SHA256
`7fe89e461d1f81ff39191dc0ae761ae807fa1fb31f6d42123a23143e4030c17e`,
preserve the evidence. The redesign is closed; do not retain dormant machinery
or replay its call-record architecture as a different local tweak.
