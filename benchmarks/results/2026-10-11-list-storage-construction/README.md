# Private list construction consolidation, 2026-10-11

List backing could remain charged when input enumeration or growth failed before
the completed list reached scalar adoption. One factory now owns population and
refunds its current private backing on failure, including after promotion. This
removes 17 production lines and the temporary reference array used during
small-list promotion. Numeric fees, scalar maps and large-payload owners remain
unchanged. The short controls support this correctness and simplification change;
they do not establish a speedup.

## Source and demonstrated defect

The prior delivery is `06be702b70111527ecae75ad0c46ca9ab373ee33`, production tree
`02f1fa7cc0f4653f839375518cef997c6662f7ed`. Frozen baseline producer
`62c3accaeadfec0a6a47d57b50235f585aeabf4e` has that identical production tree
and the four unchanged refund reproducers. Candidate implementation is
`d1b8003c5a8d0d09a07e4359d36a2c8b373cf137`, production tree
`fed483544e408ec958b2c9f239cbf3bfbcc06f20`, tests tree
`2e9a48336baf211cde8bf560e553ab600b2cbfff`. Benchmark/probe source trees are
unchanged. [evidence.json](evidence.json) preserves actual producer identities,
binary and TRX hashes, all timing attempts and cleanup receipts.

All four original private-construction checks fail on unchanged production in
both Debug and frozen VM Release. A separately retained list owns 256 bytes;
failed lazy/small-sized construction leaves 448, sized-array construction leaves
640, and denied incremental growth leaves 2,880. Expanded Debug checks additionally
pin later failure after promotion and underestimated source counts: seven of
twelve fail before the change, and all twelve pass afterward.

This is an internal storage defect. Ordinary public `list(generator-error)`
already completes its 5,000 caught failures at 1 MiB, and remains successful.
Live extension still retains its successfully inserted prefix and its charge.
No budget is increased and no retained-value control is weakened.

## Simplification and validation

`PyListStorage.Create` now creates funded empty storage and handles sized/lazy
population at one boundary. Separate incremental creation and governed enumerable
storage constructors disappear. Fixed-length object arrays retain bulk copying;
other sources preflight actual growth if their initial count changes. Promotion
copies the eight existing references directly into preflighted array capacity.
Scalar-adoption cleanup remains separate because it owns different funding.
No owner hierarchy, option, fee array or new tracking mechanism is introduced.
[SPEC.md](../../../SPEC.md) records private refund and live-prefix behavior.

- Full matching-Probe Debug: **9,972 passes** (1,921 white-box, 8,051 public).
- Focused Debug: **110 passes**. Frozen candidate VM Release: **195 passes**.
- Frozen baseline Release: **183 passes and the four expected regression failures**.
- Both producers verify all five comparison workloads and match three pure CPython
  probes in each actual execution mode at 1 MiB. Local Debug matches those probes too.
- Checks cover exact refund with other live storage, late failure/promotion,
  changing count, scalar admission denial, retained identity, clone/growth,
  partial live extension, slices, CSV, reclamation and bounded projection.

## Short diagnostic controls

Two fixed passes of five controls produce 10 collections and 422 protocol attempts,
including 210 measured batches. Every measured group completes in **7–8 seconds
including cleanup**, below its 30-second hard deadline. Correctness validation
groups take three seconds each. Worker processes retain ordinary tiering, GC and
all guards; only the supervisor disables tiering and removes that override before
launching workers. No retry collection or ten-minute lane is run.

Candidate median change relative to the baseline; negative means faster:

| Control | First pass | Second pass |
|---|---:|---:|
| Empty entry | −5.76% | −2.34% |
| Integer loop | +0.36% | −0.51% |
| CSV discard | +0.21% | −3.85% |
| Stable sort | +4.61% | −1.48% |
| List copy/clear/promotion | −2.41% | −0.05% |

Sort's candidate spread reaches 16.6%; CSV spreads also vary. These are diagnostic
results rather than qualified workload ratios. The removed promotion array is
source-proven; the timing protocol does not record allocated bytes, so this report
does not assign it a measured allocation saving. Non-array population's growth
check remains a possible small cost. Producers stay clean and rehashed, recorded
workers/groups are gone, parent handles stopped and the VM lease available.

Forced-GC relief and mutable scalar identity maps remain pending work. They cannot
be removed solely on the strength of private-construction cleanup.
