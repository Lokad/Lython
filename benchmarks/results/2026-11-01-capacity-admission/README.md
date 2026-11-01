# Capacity-owned dictionary headroom (B27)

Measured 2026-11-01 on Windows 10.0.26200, Intel i7-14700KF, 28 logical
processors, pinned .NET 11 RC3. Baseline **3dff19c3** (runtime a5150449);
candidate is that source plus [source.patch](source.patch), identified by
[acceptance.json](acceptance.json). PythonRe remains the optional 0.4 adapter.
Core has no runtime NuGet dependencies.

This removes an accounting mechanism. It is **not a qualified latency improvement**
or a new CPython comparison. Per-entry accounting was a demonstrated source
structure, not an established dominant CPU cost.

## Fixed complete-job comparison

| Workload | Input | Before B/job | Candidate B/job | Identical-before control B/job | Change vs before / control |
| --- | --- | ---: | ---: | ---: | ---: |
| Config read/edit/write | 1,024 paths / 7,177 bytes | 928,675.2 | 928,651.2 | 928,675.2 | -0.003% / -0.003% |
| CSV helper aggregation/report | 4,096 records / 57,361 bytes | 9,577,360.0 | 9,544,568.0 | 9,577,360.0 | -0.342% / -0.342% |
| Text helper aggregation/report | 3,072 lines / 35,840 bytes | 5,520,264.0 | 5,520,243.2 | 5,520,259.2 | effectively unchanged |

One fixed before/candidate/identical-before sequence, four warmups, seven rounds
of five complete jobs per case, process-wide allocated-byte medians. Existing
scripts and frozen catalog; no new workload family. Public synchronous `Run`
includes fixture reset/staging, real mediated file operations, complete stdout
and file-digest checks, result disposal and accounting checks. Compilation and
process startup are excluded; reflection and validation overhead are common.
Every job returns to its compiled baseline; compiled disposal returns to zero.

The predeclared gate required mechanism deletion, semantic/ownership acceptance
and no complete-job allocation increase above 5% against either baseline. It
passes. Raw [before](before.json), [candidate](after.json) and
[control](control.json) retain every sample and assembly identity.

Median milliseconds, before/candidate/control: config 4.374/3.557/3.724;
CSV 14.860/13.626/14.044; text 15.799/14.914/16.010. Config and CSV baseline
controls drift 14.85% and 5.49%; text control drifts 1.34%. This short local
diagnostic is not an embedding-latency qualification. CPU time was not recorded;
do not infer CPU or end-to-end improvements from these wall samples. No retiming
or favorable control selection occurred.

All three owned processes exited zero and were joined within their 30-second
bounds, **7.084 seconds total**. Their exact process observations are retained.
No VM lane, conditioning scheme or producer-readiness polling was added.

## Removed machinery and contract

`PyDict` loses its separate entry counter, initial-entry charging, entry release
helpers, per-entry reserve/commit/refund paths, identity-only storage conversions
and unused bookkeeping snapshots/arguments. Existing backing capacity owns
32-byte table estimates plus 128-byte conservative pair headroom per retained
slot. Protocol metadata and large payloads retain their independent real owners.

The instance attribute ledger now funds actual retained native capacity with
32-byte table estimates plus 64-byte value headroom. Namespace views borrow the
actual instance/global storage; no new scope, ledger or accounting mode is added.
Growth admits replacement overlap before native allocation. Structural-version
and publication-finally behavior survive removal of their accounting branches.

Deletion and clear retain reusable capacity/headroom. Free slots can accept
entries under exhausted allowance, while growth must refuse before publication.
After refusal, even prepaid replacements stop through the same terminal latch.
Native cleanup remains available. Clear no longer refunds and readmits table or
name-index capacity after visible mutation. Unused slots can cause earlier denial;
exact tiny-budget thresholds and refunds on arbitrary reference withdrawal are
not promised. Escaping aliases and pending users retain funding.

## Accounting and focused validation

The existing short non-timing ownership diagnostic runs the complete ordinary
[CSV script](ownership/csv-summary.py) once per frozen assembly over the same
4,096-row input. Peak execution accounting rises **8,420,669 -> 8,421,501 bytes**,
only 832 bytes (0.010%). Complete goldens and baseline/zero cleanup pass.
[Baseline](ownership-baseline.json), [candidate](ownership-candidate.json) and
process records are retained. Each owned process is joined within 10 seconds.
The raw catalog case ID names its helper golden; the supplied script and its
recorded SHA256 identify the ordinary script actually executed. These are
conservative charges, not CLR heap/RSS measurements.

Focused Release checks pass **938 native / 2,748 public cases per framework**.
They retain Python key identity/order, custom callbacks, namespace behavior,
physical key release, escaping views, terminal refusal, pre-mutation denial,
cooperative cancellation, suspended ownership and shared-budget cleanup.
New native controls fill prepaid dictionary/attribute slots under pressure,
reject growth without mutation and preserve the first refusal on replacement.

Accounting assertions now expect retained capacity. A deletion lookup constructs
one additional batch-owned slice shell; physical key collection is still checked
independently. The initial mixed net11 focused log preserves eight superseded
refund assertions; the corrected native run and both-framework public checks
pass. No containment assertion or timeout is relaxed.

Four finite fixtures need explicit headroom for the new contract: four retained
wrapper tables use 128 instead of 64 KiB, the 3,000-entry construction uses 2
instead of 1 MiB, and the 5,000-entry clear/refill case uses 3 instead of 2 MiB.
The original 2,000-construction JSON subclass stress fixture uses 6 instead of
4 MiB and adds an explicit peak bound; it passes both APIs/frameworks.
Their original workloads, output assertions and repeated-cleanup checks remain;
wrapper tests now also require the separate execution allowance to return to zero.
Bounded host-driven streaming uses the existing ownership boundaries rather than
cumulative allocation accounting.

Full delivery qualification is recorded separately in [VALIDATION.md](VALIDATION.md).

## Reproduction boundary

The saved driver takes a built benchmark assembly, variant label and decompressed
catalog. Use separately built baseline/candidate assemblies with the pinned SDK;
the saved source patch identifies the candidate. Ownership uses its separate
driver and supplied CSV script. These are immutable observations, not a new
maintained benchmark framework or an instruction to repeat this closed comparison.
