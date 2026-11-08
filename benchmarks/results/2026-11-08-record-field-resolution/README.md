# Flat lookup storage and shared record fields

2026-11-08, R119/B478. Qualified reference b1e17b64/core93e926fe versus refined
core19b8b5a5 (source inventory in identity). One flat admitted entry array replaces
individual member-cache objects. Repeated executable string field reads reuse a
position in immutable shared CSV/JSON key layouts. No new opcode, engine, owner,
public API or cache framework. Entries retain layout/key/index, never an old row
or its value. Current live-slot checks preserve deletion/detachment. Admission
precedes the layout proof; general/custom/missing reads stay canonical.

**Retain, with explicit debt.** Quiet F4as_v7/Ubuntu24.04.4/.NET11RC3, unchanged
7cf worker/host/optional0.4, normal GC/tiering. Two processes per core,5s warmup,
7rotated150ms rounds,8complete script/API jobs,224.323s. Ranges compare both
candidate medians to both unchanged reference medians, not confidence intervals.
Mediated RAM-file I/O, validation and prior-result cleanup are included; compile,
startup/staging/reset and final cache teardown are excluded. Full stdout/files,
source/core/auxiliary identity, ownership balances and quiet gates pass.

| Complete workload | Elapsed change | Process CPU change | Assessment |
| --- | ---: | ---: | --- |
| Integer CSV | −5.19..−8.86% | −0.44..−9.38% | Supported improvement; candidate spread3.59% |
| Integer JSON-lines | −8.12..−9.04% | −10.00% | Supported improvement |
| Float CSV, sync | −5.65..−10.38% | −3.70..−13.19% | Supported direction; reference spread4.05% |
| Float CSV, async | −6.42..−7.51% | −3.85..−7.69% | Supported improvement |
| Custom-operator CSV, sync | −3.11..−6.41% | −6.25% | Supported improvement |
| Custom-operator CSV, async | −4.34..−6.07% | −6.25..−7.14% | Supported improvement |
| Text control | −2.94..+1.80% | −4.76..+2.44% | Inconclusive |
| Report control | −4.33..+3.19% | +1.53..+6.25% | Elapsed inconclusive; possible CPU debt |

Normal same-process allocation diagnostics show ordinary CSV/text/report near
neutral, **custom CSV+0.575%** (~65.7KB) across APIs, and compilation allocation
unchanged. Flat entry arrays use24B/slot versus8B reference slots plus former
individual objects. Mutable self.value never caches a value but still admits a
cold table;4096*16B explains~64KiB of the custom increase. Accept this small debt
with measured complete-script gains; inspect deletion of never-eligible receiver
cache storage next, without an ownership redesign. No general allocation win.
Desktop timings remain diagnostic, including adverse text/custom rows. Do not
subtract different source pairs or infer CPython ratios from this comparison.

Initial core138b6a09 allocated a field-cache array even in once-only helper bodies:
report allocation+9.02%. Census corrected an earlier mistaken source inference:
render has13ordinary instructions, not a native-return body. Its3new slots made a
96B array per call;3072*96B explains the~295KB increase. The bounded refinement
allocates record-field slots only in existing loop/eager-comprehension bodies.
Report allocation returns near neutral, with zero helper field-cache slots.
Initial source/core/raw, mistaken interpretation and all adverse results survive
in local-evidence.tgz and separate initial summaries. No favorable-only retiming.

Refined source passes33internal/14public Release per actual11/10, including
admission callback mutation/deletion/clear/cancellation/refusal, independent rows,
changing keys, actual suspended item protocols and reusable shared allowance.
The earlier broader455internal/3034public per target accidentally included Stress;
those results remain, without claiming that they qualify the refined source.
Independent isolated CPython semantic assertions pass. Full coherent platform,
original Stress and installed-core package delivery is pending, not yet claimed.

All VM users joined, temporary fixtures removed, owned RAM unmounted and persistent
raw/archive verified under the lease and independently locally. Live finite270s
unit evidence is archived; terminal unit infinity reflects its subsequent unload.
Archive SHA256:0afdda174721c6db68f52c19886c11a42a38061683e987d9f2de7d690e8bb3f5.
No startup/RSS/embedding-tail/pending-I/O or publication claim. The performance
objective and independent regex track continue; no replay of older qualification.

[Controlled assessment](assessment-vm.json), [allocation diagnostics](assessment-local.json),
[compile diagnostics](compile-assessment.json), [identity](identity.json),
[receipt](archive-receipt.json), [raw VM evidence](observations.tgz),
[initial allocation/timing](initial-assessment-local.json), [initial compile](initial-compile-assessment.json),
[all local sources/binaries/logs](local-evidence.tgz), [source assessment](source-assessment.md).

The preceding [R118 source gate](mapping-source-gate.md) selected no guarded
get/add/store syntax fusion: it needed new conditional opcode/CFG/local proof
machinery plus the original sequence. No measured failure/permanent ban is inferred.
