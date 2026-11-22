# B1337 Reuse validated regex group metadata

Private `ed5e4b527` retains the frontend's validated named-group map
with each owned compiled pattern. Runtime wrappers reuse that map and
copy guest-visible strings into the active execution owner. This deletes
repeat provider reads and UTF-8 validation for precompiled patterns.
Dynamic compilation keeps its existing checks. Empty frontend metadata
uses a shared empty map.

Compared `e961df912` with `ed5e4b527` on November 22, 2026 UTC on the
quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. Each revision
used two workers with at least twelve seconds of warmup and seven
rotated batches apiece. Ranges span the four process-median pairings.
Warm time includes output verification and prior-result disposal.
Time to result adds compilation and code-owner disposal. Startup and
final teardown are separate. All cases use the same optional adapter.

| Complete script | Boundary | Elapsed change | Process CPU change | Elapsed spread reference / candidate |
| --- | --- | ---: | ---: | ---: |
| 64 named rules | Warm | −3.99 to −0.34% | −4.42 to +0.35% | 3.58 / 0.22% |
| 64 unnamed rules | Warm | −2.22 to −0.17% | −2.70 to −0.78% | 1.91 / 0.18% |
| Unicode group report | Warm | +1.11 to +1.67% | +1.54 to +3.32% | 0.17 / 0.38% |
| ASCII group report | Warm | −0.90 to +1.28% | −1.60 to 0.00% | 0.95 / 1.24% |
| 64 named rules | Time to result | −1.03 to +1.51% | −0.84 to +1.41% | 1.25 / 1.29% |

Local CLR allocation falls 0.87% for the named rules and 1.88% for
the unnamed rules. Allocation for the group reports is effectively
flat, admitted peaks match and all execution allowances refund.
The source pair does less provider work. Its Unicode group report
uses consistently more CPU and the named-rule time to result spans
zero. Keep the candidate private. The comparison does not isolate
the cause of the group-report regression. Revisit metadata sharing
if it can also eliminate guest name copying or repeated indexing
while retaining stable identity and funding.

Windows .NET 11 and Ubuntu .NET 10 each pass 67 white-box and 425
public Release checks. They cover group order and names, disposal,
active cancellation and distinct compile and execution allowances.
Both 32-file images pass four CPython goldens and all 140 timed
batches verify. Every worker joins and the persistent archive verifies
before lease release. Staging retired only exact older working copies
whose contents survive in pushed archives. The initial read-only
search found no eligible large observation archives to retire.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Sources and checks](checks-and-procedures.tar.xz).
