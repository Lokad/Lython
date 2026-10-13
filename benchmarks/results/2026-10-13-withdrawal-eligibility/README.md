# Rejected reference-withdrawal retry rule

Assessment on 2026-10-13 at `9126cfc4dd84f53ef941434b0a9d9675a4b5bc8d`,
production tree `7bbf8219299cd52347f74464d20ea2d08ddb0c0e`, Windows x64,
.NET 10.0.12 Release. This is a bounded policy prototype, not a shipped runtime
change, CPython comparison or embedding-latency measurement.

Simply authorizing exhaustion relief after every actual reference removal is
insufficient. Another alias can retain the same owner. Eight such removals
authorize eight full collections, while the unchanged suppression rule performs
none. Both variants correctly deny the requests and preserve all charges.

| Test-only rule | Subsequent denied requests | Additional Gen2 collections | Committed bytes |
| --- | ---: | ---: | ---: |
| Existing allocation-progress rule | 8 | 0 | 304 |
| Grant relief after actual slot withdrawal | 8 | 8 | 304 |

The [prototype source](WithdrawalRetryPrototypeTests.cs) uses the existing
governor and storage pool at a 336-byte cap. It commits one 128-byte owner,
its existing funded registration/backing and a 16-byte standalone charge.
A first 64-byte request establishes pinned exhaustion. Each later iteration
removes a real C# reference slot, while `kept` remains a strong alias to the
owner. The proposed permission is simulated with `NotePrefundedAllocation`;
no reserve, commit, refund, owner identity or production mutation hook is added.
The same 64-byte request still cannot fit. `GC.KeepAlive` validates retained
liveness through the entire control.

Both Release cases passed within a single 0.981-second owned test process,
including cleanup; the build passed in 8.832 seconds. The test assembly SHA256
was `9DF07DE5E38013619DE942BA06B0B73A4D6ABB3F8BCCE37F62C7D9F67244DDDE`.
Original process receipts, stdout and TRX remain in private
`.git/agent-notes/withdrawal-eligibility-20261013/`.

This exercises the proposed boolean permission rule, rather than a Lython guest
deletion implementation. It proves that an edge change alone does not establish
reclaimable storage. It does not measure host latency or resolve the separate
false denial after deletion of an uncollected nested tuple. The prototype is
preserved here as rejected evidence and is absent from the normal test suite;
eight collections are an unwanted outcome, not an accepted runtime contract.
To reproduce, temporarily copy the source into the white-box test project,
build Release, then filter `FullyQualifiedName~WithdrawalRetryPrototypeTests`.

The standalone retry investigation stops here. Distinguishing removal of the
last retaining path from alias removal requires complete root and child
representation, including native accumulators and pending async state. A partial
mutation flag or per-owner retry coupon would add machinery while retaining the
same dependency on forced CLR collection. Continue through the
[complete ownership invariant](../2026-10-12-memory-roots/OWNERSHIP-RULE-2026-10-13.md).
The [already-collected recovery fix](../2026-10-13-collected-recovery/README.md)
remains valid and unchanged.
