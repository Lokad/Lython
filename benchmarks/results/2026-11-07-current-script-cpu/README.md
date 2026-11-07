# Current text/report CPU gate — R24

2026-11-07 UTC, sourceec134e8c/frozen23a0a257, unchanged b94fa98c worker and normal
optional0.4 adapter. Quiet AzureF4as_v7/Ubuntu24.04.4/.NET11RC3, ordinary GC/tiering.
Existing complete scripts with mediated RAM I/O, full checks and prior-result
cleanup; compilation/startup/staging/final teardown excluded from guest batches.
Six guest-second warmups,2s settling, deadline-bounded3s windows of complete checked
single-job requests.199Hz cpu-clock/fp and PerfMapEnabled3 for maps only. No new
production instrumentation, runtime change, throughput or CPython comparison.

Corrected B371 collects5.216s of execution-boundary clocks; actual guest threads
account for2.75stext/2.27sreport and546/451samples. Quiet endpoints, actual images,
pre/post output/file goldens, Closed/exit0/final shared budget0, joined users,
fixture cleanup and persistent under-lease archive verification pass. Owner ends
00:13:34UTC. Local observation starts around00:15:51–54, after completed archival
but slightly short of proof of the conservative180s observation delay from
00:12:54 launch. Do not claim that full non-observation bound was met. This does
not erase the recorded quiet gates/joins or turn the profile into normal-clock
performance qualification. Keep that procedural deviation explicit.

| Selected exclusive leaf group | Text /546samples | Report /451samples |
| --- | ---: | ---: |
| Instruction prefix including inlined work |21.06%|5.99%|
| Ordinary calls/binding |2.01%|2.00%|
| Native guards |1.47%|7.10%|
| Native evaluation |2.01%|8.65%|
| JSON string/container reading |—|9.98%|
| Formatted append |—|3.33%|
| Local result publication |2.01%|1.11%|
| Monitor entry/exit |4.95%|4.88%|
| Exact integer addition |3.11%|0%|
| Unknown |4.76%|6.87%|

No malformed map lines in the corrected capture. Small sample counts/inlining,
tiering, transport and profiling effects limit attribution. Necessary semantics
are included; prefix21% is not a removable dispatch fraction. Do not subtract
shares from R19 or forecast gains. This supports neither a fresh call arena nor
universal scalar representation on current evidence. Text remains open.

## Concrete next source decision

CSV source inspection distinguishes public reader adoption from DictReader.
Private rows already avoid PyList, but DictReader still allocates dictionary
values and publishes every field from a separate funded row array. R20's shared
key metadata now permits a different storage-elimination model: directly adopt
the completed array for exact unchanged unique native headers, with the same
canonical dictionary representation and existing execution owner. This is not
parse-time fusion or a revival of the former bulk constructor.

Before code, prove transfer/refund accounting, independently mutable rows,
deletion/clear/growth/copy, mismatch/duplicate/non-string/missing/excess fields,
shared-reader reentry and admission-hook header mutation. Admission must precede
shell allocation, retain the row until transfer and revalidate mutable headers
after admission. No refund/re-reserve of still-retained storage, new owner or
fallback after refused admission. Existing general protocol path remains needed.
Select only after that bounded ownership/source proof; compare complete ordinary
and native CSV/reader plus unaffected text/JSON controls on the same baseline.
This is a hypothesis, not evidence that CSV construction dominates or a speedup.

## Original failed conditions

B370 captures text, then perf report refuses a file chowned before privileged
reads: exact message is 'not owned by current user or root'. Missing postchecks/
quiet/normal worker close keep it incomplete. Preserve its raw trace/maps, journal,
source and failed receipt. Refine only export ownership order: move transfer after
all perf reads. First B371 preflight still names existing B370 in one split path
literal; it stops before starting users. Preserve failed launch, correct that
literal and then collect the unchanged runtime once. No failed sample is relabeled
qualified, condition hidden, runtime timeout relaxed or arbitrary retry campaign.

Disk headroom: retire exact expanded old text-warm6,287,952B and text-compile
3,151,400B only after equality with independently committed/pushed raw archive
93074207, exclusive lease, ended users and no dependencies. Keep all raw archives,
maps, sources and binaries. Receipts include original member hashes.

[Assessment](assessment.json), [corrected raw/source/gates](observations.tgz),
[archive receipt](archive-receipt.json), [original incomplete capture](b370-failed-observations.tgz).
Whole-script objective stays open; no new full qualification follows from profiling.
