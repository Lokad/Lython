# Native instructions over admitted bound storage

2026-11-14UTC, R339frozen38c0562f over R338c48c93ae/frozene2482e0b.
**Retain the eligibility/transport separation.** 637internal/1562public focused
Release cases pass per actual.NET11/10. Full combined R338/R339 platform, original
stress and installed-consumer delivery[passes43545993574](delivery/README.md):
581inputs/16summaries cached. Publication/tagging remain postponed.

The previous compiler fact mixed native-opcode eligibility with the sixteen-slot
private call-buffer capacity. Bound calls already admit their complete locals and
operand array. Separate the predicates and run eligible larger bodies directly
over that existing span. The private stack buffer, argument-binding restrictions,
lexical/exception/opcode guards and managed nesting limits remain unchanged.
No new allocator, copy, pool, owner, value representation, execution route or
public option. The ordinary deferred instruction prefix remains necessary for
unsupported operations. Transfers preserve the exact PC, live values and owners.

This does not remove a materialized frame: the prior ordinary prefix already
defers contexts. It broadens use of the existing native dispatch and arithmetic
chain helpers. Four desktop allocation processes, ordinary GC/no forced collection,
show **7,883,991 B versus7,108,022–7,108,026 B per complete fulfillment script,
−9.84%**. The small-while control stays5,131,647 B. Counters include fixture reset,
checks and cleanup. Allocation, CPU and elapsed are separate observations; no
heap/RSS, process-tail or universal speedup follows.

## Acceptance

The representative4096-row CSV validates nine fields, applies quantity/availability/
pack rules, computes prices/discount/tax/shipping, aggregates eight SKUs and writes
a checked CSV report. CPython3.13.2 independently supplies exact stdout/file
goldens. Frozen-core inspection proves its price helper has the same85instructions
and23slots before/after: old native=False, candidate=True. The small while helper's
facts are unchanged. Physical backward block edges are not a claim about Python
loop count. Both sources independently pass frozen-worker output and budget reuse.

Eleven new public cases cover20locals,20/80arguments, dynamic keywords/live defaults,
binding errors, aliases, retained formatted results sharing an allowance, actual
asynchronous file work without replay, cancellation of empty nonallocating loops,
terminal refusal before body effects and pending file cleanup. A partial formatting
builder survives a custom __format__ callback that suspends; cancelled cleanup
returns to baseline after joining. Related managed-stack/recursion controls remain.
Initial80-term fixture failed the existing64-operand frontend guard: split the
fixture expression into40-term groups, leaving the guard unchanged. Original
source/log/TRX remain in the archive; no runtime failure is relabelled.

## One bounded whole-script comparison

| Complete script / phase | Elapsed change | Wider CPU change | Process spread candidate/reference | Max IQR |
| --- | ---: | ---: | ---: | ---: |
|csv-fulfillment.large /warm|-13.73..-12.88%|-13.97..-12.28%|0.47/0.50%|1.21%|
|csv-fulfillment.large /compile-run|-16.09..-14.57%|-16.07..-14.29%|0.40/1.40%|0.89%|
|csv-check-digits.large /warm|-2.57..+1.50%|-4.65..+1.37%|0.37/3.79%|0.87%|

B709three jobs finish142.262s under one300s owner, quiet
AzureF4as_v7/Ubuntu24.04.4/.NET11RC3, ordinary workstation GC/tiering. Fixed B685
worker and optional0.4adapter are identical; two fresh processes per revision/job,
eight-second full-request wall warmups, five rotating0.5s wall-sized batches.
The complete fulfillment script runs12.88–13.73%faster warm and14.57–16.09%faster
compile-to-result, with wider CPU support. Retain this source-specific result.

The small-helper control spans−2.57..+1.50%elapsed and−4.65..+1.37%wider CPU;
reference process spread3.79%limits attribution. Preserve this control uncertainty
and every adverse point, rather than claim neutrality or repeat unchanged timing.
The strong affected result plus source simplification and allocation reduction
justify retention. No external baseline was timed in this collection.

Warm guest clocks include mediated RAM-file I/O, full stdout/file validation and
prior-result cleanup; compilation/startup, fixture reset/protocol and final cache
teardown are excluded. Compile-to-result adds compilation/compiled-owner disposal.
Wider CPU includes reset/protocol. Ranges span process medians, not confidence
intervals. All identity/golden/balance/quiet/join/archive gates pass. Persistent
rawSHA9c48356edb2ff46179aa37b22b690c8829fe561d13edf084478c79cada9f5968; no workers or RAM fixtures remain. No evidence or
current image was retired and no VM floor relaxed. These two useful eligibility
milestones do not complete the whole-script performance objective.

[Timing/wider CPU](summary.json), [allocation](allocation-summary.json),
[IR/source inspection](inspection.json), [raw](observations.tgz),
[exact frozen sources/bits, initial failure and focused checks](source-checks.tar.xz).
