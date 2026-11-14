# Native tuple returns over admitted bound storage

2026-11-14UTC, R353. Prototype frozen `9cfb7d5b`, three production inputs changed
from qualified7e2f7cac/frozen72c9. Runtime/evidence commit recorded subsequently.
**Focused correctness and allocation checks pass; CPU comparison and full
Windows/Ubuntu/stress/installed-consumer qualification remain pending.**
Publication/tagging and optional-adapter promotion remain held.

Larger eligible native helpers already execute in their admitted bound arrays,
but their owned entry supplied no direct-unpack destination. They therefore
constructed a temporary dynamic tuple before an immediate exact unpack. Reuse
the existing convention and share caller preparation/publication/retirement with
the small-helper entry. No new result protocol, frame, owner, arena or memory policy.
Eager tuple admission/conservative element funding remain. Child cleanup finishes
before target publication; failure tracks all output references. Real suspension
uses the ordinary native continuation and retains no borrowed caller span.

10internal/181public focused cases pass per actual .NET11/10 (382total), including
aliases/defaults, target mutation order, mismatched unpacking, actual delayed I/O,
cancellation, terminal refusal and shared-budget reuse. Nine isolated CPython3.13.2
goldens match. Initial private Python extractor accidentally included an unrelated
expected-value array; original script and correction survive. No semantic assertion
or runtime condition was relaxed.

Existing whole-script allocation tooling, fixed worker/adapter/catalog and two
fresh processes per source, reversed role order, normal GC/no forced collection:

| Complete script | Reference B/invocation | Candidate B/invocation | Change |
| --- | ---: | ---: | ---: |
| CSV fulfillment,4,096records |6,775,007.875|6,316,237.625|−6.77%|
| CSV invoice,4,096records |5,245,606|5,245,602.125–5,245,609.5|Effectively flat|
| Captured text,3,072lines |447,869.75|447,869.75|Flat|

All independent stdout/files, image hashes, budget baseline and joined-user checks
pass. Invoice already used the small direct-return entry, so its original hypothesized
gain does not appear. Absolute totals depend on harness/path; compare only within
this collection. **No CPU or elapsed improvement is inferred.** One bounded quiet-VM
warm/fresh comparison remains, with unchanged controls and adverse results retained;
full qualification follows a coherent delivery checkpoint. The current CPython guide
still measures the separately qualified7e2/frozen72 source. This does not complete
the broader whole-script performance objective.

[Decision](decision.json),[allocation](allocation-summary.json),
[sources/core/tools/checks/observations](sources-checks-and-allocation.tar.xz).
Reference sources/bits and full qualification remain in
[R351](../2026-11-14-compiled-literal-tuples/README.md).

Subsequent [B725comparison](comparison/README.md) completes all gates: fulfillment
warm−1.25..−3.13%/fresh−1.12..−4.65%, with modest wider CPU support. Invoice
also improves; variable text controls remain uncertainty. Original collection-time
pending status above is preserved. Full delivery is still pending independently.

Measured source subsequently [passes full43583650094qualification](delivery/README.md).
Collection-time pending metadata/adverse evidence remain unchanged; later CSV work
is separate.
