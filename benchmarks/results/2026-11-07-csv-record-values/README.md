# Completed CSV record values — R25

Retain direct transfer of completed, already-funded CSV row arrays into canonical
shared-key dictionaries. Exact row width and header key identities must match;
revalidate after shell admission callbacks can mutate/reenter. This removes the
second values allocation, field copying/publication and private-row retirement
on that path. No parser fusion, new backend, owner/refcount, pool, policy or public
option. First/changed/duplicate/non-string/missing/excess headers keep required
general construction. Existing row and dictionary representations remain canonical.

The dictionary takes the original conservative capacity charge; no refund and
re-reservation of live storage. Values are independent across rows. The capacity
can be shorter than rounded key metadata; existing mutation/copy/replacement uses
actual values capacity. Original row headroom64+80n is retained, versus32+72n for
native values of the same actual capacity. Earlier denial is possible for some
shapes; this is an intentional simpler ownership contract, not exact heap tracing.
Clear retains backing capacity/funding. Execution/result ownership cleanup returns
the allowance, independently of CLR collection.

## Allocation and controlled complete scripts

2026-11-07UTC, baseline a48caba2 evidence/ec134e8c runtime, frozen23a0a257;
candidate0933d540 is the archived45-line production migration. Exact hashes/source
snapshots remain in identity/evidence. Normal optional0.4 adapter, no producer or
core dependency change. Do not transfer results to an unrelated revision.

Existing eight-case complete-job allocation driver, both APIs, three isolated
images sharing CoreLib/key hashes, ordinary GC/tiering: native-helper CSV
3,138,183.5→2,908,886.5B/job sync (−7.31%);3,499,861→3,270,542.5B async
(−6.55%). Text/slice controls flat; minor other process-allocation differences
remain raw. Goldens, hash/image isolation, terminal balances and fixture cleanup
pass. Admitted execution peak3,737,138→3,671,314B on this fixture; neither admission
estimates nor process allocation are heap/RSS or embedding-latency measurements.

Quiet AzureF4as_v7/Ubuntu24.04.4/.NET11RC3, unchanged b94fa98c worker/host.
B372108.62s, five complete jobs,5s warmups/role/seven rotated150ms rounds;
reference/current/identical current. Normal GC/tiering. Ready images, all goldens,
terminal funding, quiet endpoints, Closed/joins, fixtures and under-lease verified
persistent archive pass. No SSH before the150sowner+30sstop bound plus10s margin.
Boundary includes invocation, mediated RAM I/O, full checks and prior-result
cleanup. Startup/staging/final teardown excluded; no fresh/async elapsed or new
CPython ratio was measured in this source pair.

| Warm boundary / control | Reference | Current / identical current | Elapsed change |
| --- | ---: | ---: | ---: |
| CSV native helper |4.0815ms|3.7663 /3.7533ms|−7.72 /−8.04%|
| CSV ordinary helper |4.1049ms|3.9355 /3.9088ms|−4.13 /−4.78%|
| Public csv.reader control |2.4799ms|2.4691 /2.4533ms|−0.44 /−1.07%|
| Text control |2.1667ms|2.0930 /2.1459ms|−3.40 /−0.96%|
| JSON compilation control |64.551us|64.487 /63.633us|−0.10 /−1.42%|

Affected CSV role spreads0.35/0.68%, within-role IQR below0.72%. Text spread2.52%
and moving controls limit exact attribution. Coarse process CPU is mixed, including
an adverse ordinary-helper current role; no general CPU gain or causal repair of
text debt is claimed. Retain coherent ownership/storage elimination with measured
allocation and bounded elapsed evidence. Whole-script performance work remains open.

## Correctness, original failure and delivery

Release solution0warnings/errors. Focused125internal/389public per actual.NET11/10
pass. Six new controls preserve independently mutable exact-capacity rows, copy/
growth/clear and retained values, header identity/width changes during admission,
cancellation/refusal before transfer, input funding and final shared-budget reuse.
Existing CSV protocol, mutable headers, short/rest fields, real suspension/cleanup,
refused admission and retained results remain. Explicit Release installed consumers
pass both actual runtimes, including bounded batches and real asynchronous I/O.
Full platform/Stress/package delivery remains a separate upcoming checkpoint.

Initial123pass/2fail are the new Clear tests' incorrect expectation of a refund.
Source review confirms ordinary dictionary Clear retains its arrays; the nearby
ClearSetStorage relinquishes capacity only for sets. Correct test/source gate to
require empty contents with exact unchanged funding, preserving the original
failure and design. No production clear/refund behavior or timeout was changed.
Private postprocessing first has a PowerShell quoting SyntaxError before SSH;
replace it with a parsed script file. No guest execution/measurement repeated.

VM headroom: retire20 exact expanded B371 perf files only after verifying every
hash against independently committed/pushed a48caba2 archive db77fc61, its remote
copy, ended users, lease and no links. Keep both raw archives and all binaries.
Physical fixture/catalogs are unchanged; native/public-reader cases reuse the
prior frozen CSV catalog, deduplicating identical source/golden IDs. No new
benchmark framework, broad audit or repeated producer qualification. Publication held.
