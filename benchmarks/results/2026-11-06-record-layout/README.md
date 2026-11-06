# Shared record keys — R20

Retain this coherent redesign, accepting possible text regression as explicit
debt for the next whole-script milestone. It does not complete the performance
objective. Split the canonical native dictionary table into immutable-capable key metadata
and independently mutable values. Matching JSON/CSV records share keys; sets
omit values storage. Delete the coupled entries, sorted JSON hint, comparer and
matched-key bookkeeping. Keep the namespace bridge and real protocol snapshots.
There is no additional mapping backend, policy, pool or ownership subsystem.

Record values grow from actual input, independently of a prior wide schema.
Existing-value updates, deletion and clear need no metadata admission. Insertion
admits private replacement before mutation, retaining shared layout/value funding
through construction. Protocol mutation conservatively promotes before equality;
admission can refuse earlier. Shared metadata remains charged to the existing
execution/result owner, including superseded layouts and retained aliases.
Sparse shared rows can scan vacant positions; no separate occupancy ledger exists.

## Complete scripts

2026-11-06, quiet Azure F4as_v7 / Ubuntu24.04.4 / .NET11 RC3 / CPython3.13.16.
Same b94fa98c worker/protocol and normal0.4 adapter/producer. Reference runtime
b542b601, VM core edf02bae; candidate db55033e plus R20, core67f84196.
That runtime source is now committed as f2ce53db; later documentation/build
metadata does not replace the recorded binary identities or timing boundary.
Exact sources/binaries/catalog identities are in the raw archive. Source changes
are in source-and-check-history.tgz; focused-results.json records both runtimes.

B361 completes140.25s, exit0. Every golden, immediate quiet endpoint, worker join,
fixture cleanup and under-lease archive/verification gate passes. Warm excludes
compilation; fresh includes compilation/program disposal. Both include actual
mediated RAM I/O, full validation and prior-result cleanup. Process startup,
fixture staging and final teardown are outside clocks. Five-second warmup/role,
seven rotated150ms batches and two identical candidate processes; no SSH or
profiler during collection. This is an elapsed source pair, not an allocation,
RSS, embedding-latency or suspended-I/O measurement.

| B361 script / lane | Reference | Candidate / identical control | Elapsed change | CPython |
| --- | ---: | ---: | ---: | ---: |
| Report / warm |3.779ms|3.576 /3.446ms|−5.36 /−8.80%|2.576ms|
| Report / fresh |4.065ms|3.638 /3.719ms|−10.52 /−8.53%|2.765ms|
| JSON lines/helpers / warm |4.978ms|4.355 /4.436ms|−12.53 /−10.89%|4.100ms|
| CSV/functions / warm |4.274ms|4.169 /4.222ms|−2.46 /−1.22%|3.221ms|
| Text/helpers / warm |2.151ms|2.258 /2.139ms|+5.01 /−0.53%|1.166ms|

Report current-role differences3.64% warm/2.23% fresh, fresh IQR up to5.38%,
JSON1.87% role difference and CSV1.27% limit precision. Coarse process CPU is
retained separately; CSV CPU is adverse despite lower elapsed. No claim of CSV
CPU improvement. The text control's5.28% identical-build spread makes its source
effect unresolved; retain that adverse observation. B362 completes59.44s with
unchanged code, two independent role-reversed blocks and all quiet/golden/join/
cleanup/archive gates. No SSH during capture. First block:reference2.281ms,
current2.301/2.253ms (+0.90/−1.21%). Second:reference2.108ms,current2.165/2.224ms
(+2.73/+5.53%). Current-role spreads2.09/2.73%; coarse CPU is adverse in the second
block. These observations do not establish parity or a precise source effect.
Keep the possible regression, including B361+5.01%, as accepted debt. Stop this
measurement here; prioritize the whole text aggregation/operand path next.
Report/JSON improvements and allocation reduction support retention, without
claiming that ordinary containers or text improved. No fixed-percent veto.

Current JSON warm is1.06–1.08× CPython, report1.34–1.39× warm/1.32–1.34× fresh,
CSV1.29–1.31×. These ratios describe these exact blocks, not all current scripts.
The five-script compilation/warm/fresh baseline remains separately recorded in
[B352](../2026-11-06-delivery-cpython/README.md). Do not subtract independent
medians to infer compilation cost or add historical improvement percentages.

## Separate complete-job allocation diagnostic

Precise GC.GetTotalAllocatedBytes(true), normal GC/tiering, isolated images sharing
CoreLib/hash seed, baseline/current/identical-baseline control. Existing driver:
32warmups/image, five16-job batches, eight cases, both APIs. Fixture reset,
mediated I/O, validation and prior-result cleanup included; compilation excluded.
All output/identity/isolation/funding/fixture gates pass. Busy Windows elapsed and
CPU remain diagnostic. Actual runtime source is the same pair; frozen local
baseline binary d970460e differs from the VM build's identity.

| Script | Sync allocated bytes/job before → after | Sync / async change |
| --- | ---: | ---: |
| CSV/helpers |3,599,535 →3,141,439|−12.73 /−11.57%|
| JSON/helpers |3,059,710 →2,601,134|−14.99 /−14.98%|
| Report |3,646,914 →3,302,823|−9.43 /−9.43%|
| Text/helpers |1,655,016 →1,655,093|+0.005 /effectively flat|

Scalar, fixed-text and small-configuration controls are essentially flat. The
CSV allocation workload uses the existing helpers catalog; it is distinct from
the VM's CSV/functions case. CSV admitted root peak5,670,226→3,835,730B is a
conservative accounting observation, not physical CLR live heap or RSS. Raw
allocation samples, seeds/witnesses, all8cases and admitted peaks are retained.

## Semantics, qualification and original conditions

Focused Release per actual11/10:735 internal/2,400 public. Full ordinary per
runtime:4,274 internal/16,200 public, all pass. Independent CPython golden covers
row mutation/order/key identity, duplicate/reordered/nested JSON, hooks and CSV
header edits. Real suspended equality clears/removes shared entries; cancellation
cannot be suppressed by finally-return and cleanup restores shared funding.
Refusal preserves both rows, native cleanup needs no new admission, caches retain
key funding, reentrant admission uses current data and prior wide layouts do not
force wide values. Actual shared-budget refusal/reuse and long-name raw hash
lookup are covered. No public API addition; admission thresholds can change.

Original failures remain visible:

- Small replacement initially published after cancellation during successful
  admission. Check the current token before allocation/publication; preserve its
  failing check and corrected regression. Initial scanner-delegate/compile errors
  and all build/check logs stay in source-and-check-history.tgz.
- Disk preflight inventories originally omitted four ended perf-control FIFOs;
  no data was deleted before the gate. Validate exact FIFO kinds, retired users,
  dependencies and local/remote raw archives before removing redundant expanded
  perf copies. A later headroom gate stopped setup; retire one byte-identical
  old upload with an independently verified local backup. Preserve all raw data,
  active images and6MiB setup headroom.
- B359 rejects a symlinked worker resolving the old application directory before
  any sample. Preserve failed archive/journal/status. Correct worker/deps/config
  placement to the proven hardlink layout; actual Ready core hashes pass.
- B360 completes three jobs, then a nonexistent CSV catalog ID stops collection.
  Joined users/cleanup pass, but there is no immediate final quiet gate. Preserve
  all original samples as diagnostic in its failed archive. Correct only the
  manifest ID, validate all jobs before launch and run complete B361. No runtime
  source, timeout, containment assertion or adverse result was removed.

Runtime f2ce53db is fully qualified at documentation/evidence head1fdca591:
CI42294911136 is GREEN on Windows/Ubuntu, actual11/10, original Stress and installed
package consumers. The exact head/all steps are cached in delivery-ci.json;
terminal, never query it again. Local explicit Release installed consumers also
pass both actual runtimes; the core package has no runtime dependencies and the
adapter remains nonpackable. This closes R20 delivery, not the possible text debt
or whole-script performance objective. Publication remains postponed.
