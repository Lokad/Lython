# Annotation demand — R38

Baseline79fddd4a/core8b40e46e; candidatec21c0c4b/actual.NET11, deb66c94/.NET10,
built from that source plus the recorded frontend change. Frozen images/source
are in development.tgz. This milestone changes compilation, not runtime dispatch.

AnnotationDiagnostics formerly constructed a second binding state through every
module/function/class suffix even when no later annotation check consumed it.
One temporary structural demand map removes those unused updates/state clones and
entire irrelevant annotation walks. Control-flow bodies still export all exit facts.
Scripts importing re retain full conservative flow, including the final update:
constant-pattern analysis also needs it in dead/deferred scopes. No new public API,
policy, engine, shared binding state or retained syntax metadata. The original
conservative annotation branch/return/default/class semantics remain separate from
the main analyzer's refined/reachable flow.

Focused Release919internal/2,015public pass on each actual framework.524/524
baseline/candidate diagnostic/validity cases agree, with normal optional0.4 and no
provider roles; every compilation owner returns to zero. Six existing complete
scripts pass output/file goldens, peak/final funding, retained script cleanup,
isolated images and fixture removal across compile/warm/fresh diagnostics.

Compile allocation falls4.38–8.94% on five representative scripts. Regex full-flow
control grows304B/+0.117%; warm allocation essentially flat and admitted peaks
unchanged. These precise per-process allocation observations use shared ALCs/
CoreLib/GC; desktop clocks are diagnostic, including all adverse values. A separate
diagnostic process briefly overlaps part of fresh capture. An early summary read
finds the still-incomplete JSON; the same measurement continues to successful
terminal gates. No measurement restart or CPU claim.

Initial prototype omitted regex's conservative-analysis dependency:3 existing
missing-provider tests fail after919internal/2,012public pass;522/524 differential
cases agree. Preserve initial source/image/logs and refine the semantic demand;
do not weaken the tests or silently drop frontend validation. Private diagnostic
tool's initial nullable-unboxing compile error is corrected before observations.

R37's source/census gate finds native calls already borrow contiguous slices;
dropping adjacent loads alone retains storage and needs snapshot/span metadata.
No additional call transport or general value/frame representation is selected.
Static instruction counts are not dynamic frequencies or CPU savings.

Quiet B389 completes140.29s/all5jobs/gates on the same frozen source now committed
asb0889a46. SourceLink remains79fddd4a from the precommit build; production source
matches. Report compile elapsed−6.51–8.02%, text compile−8.25–9.13%, config
compile-to-result−0.54–2.42%. Warm text−5.46 to+1.74% has4.94%baseline role spread;
it establishes neither a warm gain nor parity. Full-flow regex compile+0.014–1.66%,
current1.23%spread, is accepted possible overhead alongside304B allocation growth.
No CPU attribution or compound source-pair debt repayment. Retain the bounded
frontend change; no unchanged retiming selected. Full delivery42351006316 on
exactb0889a46 batches R36/R38 Windows/Ubuntu/actual11/10/original Stress/installed
consumers; pending at this evidence commit. R35's receipt
does not qualify this source. Publication postponed; whole-script objective active.

[Focused checks](focused-initial.json), [diagnostics](diagnostics-final.json),
[allocation/diagnostic clocks](summary.json), [source/images/all conditions](development.tgz).


## Quiet comparison provenance

Unchanged b94fa98c worker/host and normal optional0.4; dedicated AzureF4as_v7/
Ubuntu24.04.4/.NET11RC3, normal GC/tiering. Two baseline/two current processes,
5s warmup per role/job and seven rotated150ms rounds. Compile includes program
disposal; config fresh/text warm include mediated RAM I/O, complete output/file
goldens and prior-result cleanup. Fresh adds compile/program disposal; startup,
staging/reset and final cached teardown excluded. CPU tick observations stay
separate. No CPython refresh, startup/RSS/pending-I/O or embedding-tail claim.

All Ready hashes/auxiliaries, goldens, terminal funding, quiet endpoints, Closed/
exit/joins, fixture removal and archive verification under lease pass. Independent
retrieval verifiesa347ac58. Observation07:24:24UTC follows07:24:01.727783bound
(210s owner+30s stop+40s margin). Terminal inactive/exit0/no users or fixtures;
never query the service again. A local retrieval-template assertion failed after
valid payload/launcher generation; the shell then ran that launcher. Independent
remote setup verifies all inputs before work. Correct only retrieval path selection;
do not restart the collection. Original source/condition retained.

[Controlled summary](comparison-summary.json), [all raw gates](observations.tgz),
[commands and original plan](collection-commands.tgz), [receipt](archive-receipt.json).
