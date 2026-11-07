# Shared receiver-method implementations — R41

2026-11-07. Baseline production1971487a/coreeb6052d0; initialbfc2e427/f848abe1,
retained refinement5d2ca8e1/2aefaa39 (.NET11/10). Builds precede commit, with
SourceLink1971487a. Complete source pairs, exact images and source hashes are
preserved; source metadata is not a binary-equivalence claim. Both roles use the
same worker7cf37ca5, adapter27211dbe and normal PythonRe0.4 producer bits.
Core remains independent of the optional adapter and its publication.

Separate immutable implementation/signature from evaluated receiver. Immediate
executable calls borrow their existing positional operands; exposed values keep
the ordinary bound wrapper/metadata/binder. Migrate regex match methods and the
existing list.append, dict.get/setdefault, file.write, csv.writerow and string
join/split/replace/affix families. Delete ten opaque Direct* objects, their long
invocation chain, migrated capturing factories and ten orphaned guarded helpers.
The refinement also deletes the new one-caller resolver and redundant string-name
tests before the existing no-argument operation. No caller arena, backend/API,
memory layer, public switch or new quota. Real dynamic/presence wrappers and
measured native no-argument/helper-return routes remain.

## Allocation and controlled complete-script elapsed

The bounded original EventPipe capture has zero lost events:5248regex/6242JSON
allocation ticks. BoundCallable7.27%, synchronous delegate9.00% and asynchronous
delegate8.01% of regex sampled bytes identify a material construction phase;
these types are negligible in JSON. Statistical attribution is not exact counts,
CPU, live heap or proof every sample belongs to one method.

The existing complete-job driver measures approximately29.47% less regex process
allocation on both APIs, before and after refinement. JSON/text/CSV controls differ
by at most0.04%; every admitted peak is identical. Includes fixture reset, mediated
file I/O, complete output/file checks and prior-result disposal; compilation/startup
excluded. Process allocation is precise; shared-ALC desktop clocks are diagnostic.
All adverse results remain, including initial sync regex+31–81% and conflicting
refined desktop observations. They do not establish CPU improvement.

B394: quiet AzureF4as_v7/Ubuntu24.04.4/.NET11RC3, normal GC/tiering. Two baseline/
two retained processes,5s warmup per role/job,7rotated150ms rounds. The table spans
all four source-role pairings; role spreads are not confidence intervals.

| Complete warm script | Baseline ms | Retained ms | Elapsed change | Current/reference role spread |
| --- | ---: | ---: | ---: | ---: |
| Regex capture summary;1024records |2.530–2.541|2.266–2.312|−10.80 to−8.64%|2.00/0.42%|
| JSON helper aggregation |4.154–4.287|4.111–4.264|−4.10 to+2.65%|3.59/3.20%|
| Text helper aggregation |2.016–2.035|2.007–2.037|−1.34 to+1.05%|1.48/0.93%|
| CSV helper aggregation |3.943–4.008|3.935–4.036|−1.84 to+2.35%|2.50/1.65%|

Timed complete public runs include mediated RAM I/O, guest parsing/loops/methods/
aggregation, full checks and prior-result cleanup. Compile/startup/staging/reset/
final cached teardown excluded. Coarse10ms process CPU observations are retained,
not an isolated dispatch or broad CPU claim. This is not a CPython refresh.

Original B393 completes114.27s:regex−7.56–9.19%, CSV+2.05–3.41%, JSON+0.60–4.64%
and text−2.84 to+1.49%. Source inspection then finds the redundant resolver/no-arg
checks; B394 changes that work, not the acceptance rules. It completes114.47s.
Both have every Ready/core/aux/golden/quiet/funding/Closed/exit/join/fixture/archive
gate. Evidence is archived and independently hash-verified after the owned bounds.
Retain simplification/allocation and the measured regex gain. Controls remain
variable; do not claim parity, repayment of original CSV debt or a proven causal
explanation. Possible CSV/JSON debt is accepted and remains visible. No further
unchanged retiming selected; the whole-script performance objective continues.

## Semantics, cancellation and ownership

Initial actual11:276internal/1279public plus10final overlapping regressions;
actual10:276internal/1238public. Filters differ as recorded, including selected
Stress in the earlier explicit11 filter. Do not sum overlapping counts. Refined
121internal/506public per actual target pass. Independent CPython3.13.2 outputs
cover receiver rebinding, exposed identity/hash/__self__/name, default presence,
keyword/invalid-call side effects and unchanged containers. Regex selectors really
suspend through __index__; overlapping executions keep independent tokens and
receiver funding, await cancellation/cleanup and return/reuse the shared allowance.
Immediate and exposed group materialization preserve the first terminal refusal.
Four tests asserting only the old opcode's absence are removed; read/call separation
uses existing LoadCallMember and independent observable coverage remains.

Full Windows/Ubuntu, original Stress, actual11/10 and installed consumers pass
on exactfba865ef in terminal GREEN42357660230. [Receipt](delivery-ci.json) records
that coherent R40/R41 delivery separately from the focused/measurement checkpoint.
No API break, publication or performance-objective completion is claimed.

Original failed setups are preserved:wrong CSV catalog id; provider-free R35 driver
refuses the first regex program before observations; new invalid-call fixture is
statically diagnosed, then corrected behind a dynamic parameter with unchanged
Python output. Initial VM preparation catches differing local worker/adapter bits;
both new roles explicitly receive identical recorded inputs. No old snapshot is
mutated. Before B394, three exact inactive single-link c21core copies are retired
only after path/hash/user checks against the already committed R40 archive; no
archive/source/current core is deleted. Receipts retain exact recovery locations.

[Retained quiet data](refined-observations.tgz), [original quiet data](initial-observations.tgz),
[summaries](refined-comparison-summary.json), [allocation ticks](allocation.tgz),
[local comparisons/failed provider setup](local-comparisons.tgz),
[source/images/development](development.tgz), [collection commands](collection-commands.tgz)
and [focused checks](refined-focused.json) retain all conditions and uncertainty.
