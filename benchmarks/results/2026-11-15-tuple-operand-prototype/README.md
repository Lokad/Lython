# Integer tuple storage and transport — original prototype

The [refined 22e5b72c source](refined/README.md) passes
[full delivery qualification](delivery/README.md).
This page preserves the original 1b88 checkpoint; its adverse observations remain.
The [first quiet-VM comparison](vm/summary.json) confirms CSV benefit and a text
regression, prompting the bounded accessor refinement. Descriptive inherited
metadata is [corrected explicitly](vm/metadata-correction.json); raw inputs remain.

2026-11-15. **Uncommitted, not retained or fully qualified.** Reference is qualified
b58e2dc4/f304b3db; prototype core is 1b88e3c7. Exact code and binaries are archived.
This record does not describe shipped performance. Publication remains postponed.

Following [current allocation attribution](../2026-11-15-tuple-allocation-origins/README.md),
computed integer tuples copy the existing final executable slots into immutable
storage. Their item reads can carry a managed source/index through arithmetic,
removing eager integer boxing and object-array transfer. Normal observation
publishes one canonical box. Reference tuples keep their existing array backing.
No new opcode, pool, identity ledger, unsafe access or execution route is added.

The wider integer backing, metadata and eventual boxes are admitted before
allocation. A scalar borrow can retain its originating tuple until observation
or retirement; funding stays with the execution/result owner. New tuples do not
store borrowed items, avoiding origin chains. Earlier denial from conservative
capacity estimates is possible. The additional storage/borrow branches are a
design cost to assess, not an automatic simplification or CPU improvement.

| Complete script | Allocation change | Diagnostic elapsed change | Unchanged-reference spread |
| --- | ---: | ---: | ---: |
| Invoice | −8.56% | −15.66..−0.36% | 3.10% |
| Fulfillment | −5.32% | −6.09..+1.49% | 3.16% |
| Captured text | 0% | +1.34..+4.55% | 1.58% |
| JSON summary | 0% | −5.47..+6.11% | 8.89% |

Local Windows/.NET 11 RC3, ordinary GC/tiering, unchanged sources/fixtures/host.
Elapsed uses two independent processes per image, 2.5 seconds full-request warmup
per process, five rotating short batches: 61.754 seconds total. Ranges retain all
four process-median pairings. Invoice candidate spread is **14.59%**. These desktop
results support no portable CPU claim; the text adversity remains unresolved.

Allocation uses the existing separate driver: one process per image, four seconds
warmup per shape, five blocks of 64 invocations. It includes fixture reset,
mediated local I/O, complete stdout/file checks and result disposal; compilation,
startup and final compiled-owner disposal are separate. Its process-wide CLR
counter is not a retained-heap measurement and requests no collection. Elapsed
instead uses command-v2, including guest I/O, goldens and prior-result cleanup,
excluding reset/protocol/compilation/startup/final cache. Boundaries are not additive.

Focused Release checks pass on each actual .NET 11/10 target: 416 public and
171 internal, followed by six lifetime/identity cases and six construction-failure
cases per target. These include actual I/O suspension, cancellation/awaited cleanup,
terminal refusal, retained results after compilation disposal and shared-budget reuse.
The supplementary runs overlap earlier cases; do not add them as unique tests.
No full Windows/Ubuntu, original stress or installed-package cycle was run for
this prototype. All measurement users joined and fixtures were removed.

The first allocation attempt timed out after 30 seconds on the **unchanged
reference**, with no observations. Its procedure/log/remaining fixture survive.
The corrected size retains the same driver/images/catalog and five measurement
blocks, with two processes total and 75 seconds per process. This resolves a
measurement sizing error, not a failed design. No result was dropped.

At this original checkpoint, the next gate was a bounded quiet-VM source-pair CPU/elapsed comparison with text/JSON
controls, followed by an explicit retain/refine/withdraw and regression-debt
decision. Current VM staging needs an exact proved archived superseded-image
retirement before adding the new image; disk floors and qualified images stay.
Do not turn that housekeeping into a broad cleanup campaign or stop independent
regex/core development. The whole-script performance objective remains open.

[Summary and limits](checkpoint.json),
[exact sources, frozen core, tests, raw observations and original timeout](sources-checks-and-observations.tar.xz).
