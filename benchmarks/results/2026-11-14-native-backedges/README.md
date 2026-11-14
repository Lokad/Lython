# Native-prefix backward branches

2026-11-14UTC, R338frozene2482e0b over fully qualified R335cdd92944/frozenf4c4f027.
**Retain the eligibility deletion for the demonstrated whole-script CPU gain.**
637internal/1551public focused Release cases pass per actual.NET11/10; full retained
platform/stress/installed-consumer delivery is pending at the coherent eligibility
checkpoint. Publication/tagging remain postponed.

The native executable loop already supports Jump/conditional branches and checks
current cancellation/refusal before every instruction. Remove the separate
backward-branch exclusion from compiler eligibility. Existing opcode, capacity,
exception, lexical and managed-stack protections stay; unsupported operations
transfer their exact PC and live values through the existing continuation. No new
optimizer, engine, arena, typed-value representation, policy or public option.

The initial claim that this removes per-call frame allocation was **incorrect**.
BeginBoundCall already uses the ordinary deferred instruction prefix without a
materialized context for this loop. The original pre-code plan and its explicit
[correction](hypothesis-correction.json) survive. The change selects the smaller
native dispatch rather than proving a large storage removal. Four allocation
processes show5131631–5131635B versus5131559B per complete4096-row script:
only72–76B, effectively flat. Invoice control is flat. No heap/RSS/tail gain follows.

## Semantic and ownership validation

Six isolated CPython3.13.2 controls preserve while/else/break/continue, nested
loops, integer promotion, aliases, nonlocal writes, late locals observation and
error order. Initial11new cases pass on both reference and candidate Release11/10;
the expanded suite includes13new public cases. Actual I/O after native iterations
occurs once; cancellation joins cleanup and restores the shared allowance.
An empty nonallocating loop remains cancellable. Retained formatted results from
two executions stay funded. Native recursion crosses the existing continuation;
deep while recursion is checked in the isolated probe, built for each target.
All4376related checks pass; original stress scales remain for delivery.

Independent frozen-core inspection shows the exact same11instructions/6slots/
one backward branch for digit_sum: native=False at f4, True at e248. Invoice
function facts are unchanged. Each worker independently verifies exact complete
file/stdout goldens and accounting reuse before timing.

## One bounded affected/control comparison

| Complete script / phase | Elapsed change | Wider CPU change | Process spread candidate/reference | Max IQR |
| --- | ---: | ---: | ---: | ---: |
|csv-check-digits.large /warm|-19.50..-17.03%|-19.18..-15.00%|0.46/2.61%|0.99%|
|csv-check-digits.large /compile-run|-16.78..-14.80%|-16.91..-15.49%|1.06/1.28%|1.60%|
|csv-invoice.large /warm|-0.48..+1.63%|-0.48..+1.52%|1.75/0.36%|1.67%|

B708finishes141.481s under one300s owner, quiet AzureF4as_v7/
Ubuntu24.04.4/.NET11RC3, ordinary workstation GC/tiering, fixed B685worker and
optional adapter0.4. Two fresh processes per revision/job, eight-second full-request
wall warmups and five rotating0.5s wall-sized batches. Guest source, fixtures,
outputs and auxiliary worker/adapter bytes are identical across revisions.

The complete4096-row CSV job parses codes, runs a numeric digit-sum while helper,
aggregates eight groups and writes a checked report. Warm reference8.269–8.485ms
versus6.830–6.861ms; compile-run8.213–8.320ms versus6.924–6.998ms. Supporting wider
CPU direction is15–19%warm and15.5–16.9%fresh. Retain this affected-workload result,
not a universal runtime or CPython speedup. Invoice control spans−0.48..+1.63%
elapsed/−0.48..+1.52%wider CPU with1.75%candidate spread: small regression debt
remains uncertainty, not proved neutrality. All raw/adverse points survive; no
unchanged retiming is needed for this bounded retention decision.

Warm guest clocks include mediated RAM-file I/O, full stdout/file validation and
prior-result cleanup; compilation/startup, fixture reset/protocol and final caches
are excluded. Compile-to-result adds compilation/compiled-owner disposal. Wider
CPU includes reset/protocol. Ranges span process medians, not confidence intervals.
The separate desktop allocation diagnostic includes reset/checks/cleanup and uses
CLR process-wide counters with no forced GC or timing claim. No external timing,
allocation percentage, embedding-tail or safety relaxation is inferred.

All identity/golden/balance/quiet/join/archive gates pass. No VM users or RAM
fixtures remain; persistent evidence SHA verifies. No evidence/image is retired
and no disk floor is relaxed. Further operand/call consolidation remains separate;
this useful source deletion does not complete the performance objective.

[Timing/wider CPU](summary.json), [allocation](allocation-summary.json),
[inspection](inspection.json), [raw](observations.tgz),
[exact frozen sources/bits, prospective conditions and checks](source-checks.tar.xz).
