# Shared executable prefix: withdrawn prototype

2026-11-15. Three uncommitted runtime variants were tested and **withdrawn**.
The retained runtime remains a7ab41f5 /716176a8, fully qualified by43675539106.
No published performance or current-runtime speedup is claimed.

The prototype replaced the separate native opcode/read-publish/format-call
driver with the existing span-consuming executable prefix. Stack-backed small
calls and the existing owned suspension transfer remained. A routing refinement
moved exception handling outside the hot loop; a third refinement separated
owned array/cache/disposal metadata from borrowed invocation state. Actual owned
metadata was admitted before allocation and remained funded through cleanup.
There was no new resource policy, unsafe code or dependency.

| Variant | Focused Release correctness | Local elapsed observations versus qualified reference |
| --- | --- | --- |
| Initial99d16dc4 |437internal/1,759public per actual.NET11/10|Fulfillment +8.8–11.6%warm/+4.2–6.3%fresh; short helpers +20.6–25.4%; observed +10.2–15.7%|
| Routingfc905405 |437internal/1,669public.NET11|Fulfillment +7.9–15.8%warm/+5.9–12.8%fresh; short +23.6–33.1%; observed +12.1–22.5%|
| Owned metadata244b3a97 |448internal/1,669public.NET11|Fulfillment −0.3..+21.7%warm/+3.4–18.2%fresh with moving controls; short +27.4–30.2%; observed +9.7–17.3%|

The third collection finishes184.512s with all8jobs, goldens, ownership, worker
joins and fixture cleanup passing. Report elapsed is lower in this collection;
file/text/JSON controls move and do not establish recovery or CPU attribution.
All three collections use unchanged command-v2 workers/catalog/host/SDK and
independent Windows processes, full-request warmups and rotating batches.
Warm includes mediated I/O, full output/file validation and prior-result cleanup;
fresh adds compilation/compiled-owner disposal. No quiet-VM, portable CPU,
allocation, CPython, heap/RSS or embedding-tail claim follows.

The repeated helper cost remains after two changed, bounded refinements.
Withdraw this implementation rather than carrying that debt into0.11. This is
not a permanent rejection of shared execution or a fixed-percentage veto.
No proposed call arena, duplicate binder or new dispatch controller is promoted.
The qualified implementation and original internal ownership test are restored;
all589source/build inputs match the baseline archive and HEAD after explicit
newline normalization. Forced actual11/10builds precede restoration checks.
Full qualification of the withdrawn final variant is deliberately not run.
[Restoration proof and focused outcomes](restoration.json):103internal/127public
per actual.NET11/10 after forced builds. [Complete logs](restoration-checks.tar.xz).

One prototype-only array-access guard caused21.NET10post-cleanup inspection
failures. The production prototype corrected its null/default sentinel without
relaxing original assertions; both failure and correction remain. Initial setup
failures, every adverse sample, source/core and procedures are preserved.

[Initial and routing outcomes](summary.json),
[initial/routing exact sources, cores and failures](initial-and-routing-refinement.tar.xz),
[owned-metadata decision and every process median](owned-metadata-summary.json),
[owned-metadata source, core, raw controls and checks](owned-metadata-refinement.tar.xz).
The latter stores unchanged auxiliaries once under owned-metadata-refinement/reference;
its complete image manifests plus preceding cores in the first archive reconstruct
all three measured images. Publication remains held; the broader whole-script
performance objective remains open.
