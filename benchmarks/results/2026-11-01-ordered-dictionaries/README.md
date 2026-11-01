# Ordered dictionary storage: correctness and simplification (B23)

Measured 2026-11-01 on Windows 10.0.26200, Intel i7-14700KF, 28 logical processors,
pinned .NET 11 RC3 SDK/runtime. Baseline **450059be**; candidate is that source plus
[source.patch](source.patch), whose SHA256 is in [acceptance.json](acceptance.json).
The optional adapter remains PythonRe 0.4.0; core has no backend dependency.

This fixes Python ordering and removes a storage tier. It is **not a qualified
latency improvement** or an updated CPython comparison.

## Fixed complete-job allocation comparison

| Workload | Input | Before B/job | Candidate B/job | Identical-before control B/job | Candidate change vs before / control |
| --- | --- | ---: | ---: | ---: | ---: |
| Configuration read/edit/write | 1,024 existing paths; 7,177 input bytes | 927,905.6 | 928,681.6 | 928,067.2 | +0.084% / +0.066% |
| CSV helper aggregation/report | 4,096 records; 57,361 input bytes | 9,085,243.2 | 9,577,360.0 | 8,921,548.8 | +5.417% / +7.351% |
| Text helper aggregation/report | 3,072 lines; 35,840 input bytes | 5,519,547.2 | 5,520,259.2 | 5,519,715.2 | +0.013% / +0.010% |

One fixed before/candidate/identical-before sequence, each in its own process.
Four warmups, seven rounds of five complete jobs per case; medians above use
process-wide allocated bytes. Public synchronous `Run` includes the existing
filesystem fixture reset/staging, actual mediated I/O, complete stdout/file
goldens, result disposal and accounting checks. Compilation and process startup
are excluded. Reflection/validation overhead is common. Inputs are the existing
catalog, not a new benchmark family.

The predeclared allocation gate rejects deleting the tier if any complete case
grows more than 10% against either baseline. It passes; the CSV cost is retained
and visible. Full semantic and ownership acceptance is independent of this gate.
Every invocation checks complete output and file digests. Accounting returns to
its steady compiled baseline after each job and to zero after compiled cleanup.

Raw [before](before.json), [candidate](after.json) and [control](control.json)
include all wall samples and assembly hashes. Median milliseconds respectively:
configuration 2.570 / 3.805 / 3.906; CSV 18.169 / 13.270 / 15.009; text 13.369 /
15.176 / 15.043. These include baseline drift and increases; no elapsed claim,
retiming or interpretation as an embedding-latency improvement is justified.
The identical CSV baseline allocation also changes about 1.8%; both controls
remain visible rather than selecting the favorable one.

Each process had a 30-second ownership bound. Baseline returned normally; its
precise process duration was not recorded. Candidate/control took 2.448 / 1.983 s,
exited zero and were joined; [process observations](after-process.json) and
[control observation](control-process.json) retain those facts. No VM lane or
producer-readiness check accompanies this diagnostic.

## Semantics and ownership

The independent [old-runtime probe](order-probe.json) confirms deletion/reinsertion
puts key `2` in the removed position for seven/eight entries, while CPython puts
it last. Five/six entries happen to work. The former six-entry linear store /
Dictionary promotion boundary caused the difference. Frozen CPython 3.13.2
[script](order-script.py) and [stdout](order-expected.txt) check views, values,
reverse iteration, copies, JSON, kwargs and popitem at 5/6/7/8/20 entries.

The replacement uses the managed BCL OrderedDictionary on both supported targets.
It deletes the small store, threshold and promotion/copy/refund branch; no custom
hash table, package, cache tier or switch is added. Live namespace adapters still
use the existing storage interface. Index-based enumeration preserves updates of
existing values; PyDict retains structural mutation guards and protocol dispatch.
Equal keys retain the original key object and position.

Hash-table probing exposes two previously masked paths: named-tuple and
struct_time type objects need identity hashes; Counter must validate invalid keys
before its first lookup. Existing protocol tests and new grown-table controls
cover these fixes. A separate CPython [type script](type-identity-script.py) and
[golden](type-identity-expected.txt) preserve distinct types with identical names.

Initial metadata/capacity and replacement overlap are admitted before allocation.
Removing an entry releases its entry headroom, not retained table capacity.
Saturated-update tests pin all remaining host allowance *after* construction,
preserving true no-new-admission checks without the old exact 320-byte layout.
If a competitor takes refunded replacement headroom, subsequent admission may
refuse before publication; old bindings and retained tables remain funded, first
refusal is terminal and scratch refunds to zero. Success is not guaranteed by
the former allocation sequence.

One additional **non-timing ownership diagnostic**, one synchronous run per source,
uses the exact same 4,096-row CSV fixture with the ordinary csv-summary script.
Peak execution accounting rises **7,633,821 -> 8,420,669 bytes** (+10.3%). Complete
goldens and owner cleanup pass in both [baseline](ownership-baseline.json) and
[candidate](ownership-candidate.json). These are conservative charges, not heap
or RSS measurements. The old 8 MiB finite fixture is insufficient by about 32 KB;
it now supplies 10 MiB, preserving every row and output assertion. Sustained
host-driven batches still use the unchanged 512 KiB shared allowance. The copy /
union fixture uses eight construction rounds per batch rather than sixteen under
its unchanged 64 KiB allowance, retaining all operations, eight batches, cleanup
balances and retained-data rejection. This does not promise immediate recovery
after arbitrary guest-reference withdrawal.

Middle deletion in the ordered table shifts later entries. Repeated deletion
in large mappings can therefore be quadratic; this is a documented tradeoff,
not a demonstrated performance improvement for deletion-heavy workloads.
Cancellation checkpoints, guest callbacks, terminal refusal and result ownership
remain in the existing runtime paths.

## Reproduction and qualification

Use the baseline revision and apply the recorded source patch in an isolated
checkout. Build the existing Release benchmark project with the pinned SDK;
freeze its output separately for each variant. [Program.cs](Program.cs) is the
existing allocation diagnostic narrowed to the three maintained scripts.
Decompress [catalog.json.gz](catalog.json.gz) to catalog.json; its uncompressed
SHA256 must match the raw observations. The [fixed runner](run-fixed.ps1) owns
and joins each worker and refuses to overwrite observations. The separate
[ownership diagnostic](ownership/Program.cs) accepts the same worker/catalog and
the repository's csv-summary.py; it performs no performance comparison.

Focused/full Release validation and platform/package delivery results are
recorded in PLAN.md and the accompanying validation notes. Do not infer a new
CPython ratio or final regex-adapter qualification from this work.
