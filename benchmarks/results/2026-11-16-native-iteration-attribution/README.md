# Native iteration and complete-script costs

R449/B851,2026-11-16UTC. Two bounded captures, no timing comparison or runtime
change. Exact R448 measured core3bb9da5a/source snapshot and unchanged five-case
catalog from [R448](../2026-11-16-compact-iteration-result/README.md). This is before
its16-helper forwarding/unused-local cleanup in d77a8bdd; no binary equality or
claim of qualification for this private image. Its prior focused/budget checks are
recorded. Final-source delivery43908598724 remains independently active.

| Whole workload | Guest sample window | Active samples / unknown leaves | Selected exclusive mapped leaves |
| --- | ---: | ---: | --- |
| Normalization,4,096lines/52,224B |4.953s|948/105|Ordinary prefix82; RStrip50; expandtabs35; ForNext30; cursor IEnumerator27; native-call selection28 |
| Positional helper/range,16,384calls |5.123s|1,006/63|Ordinary prefix299; native prefix90; arithmetic selection89; native-call selection78; StartInline73; StartNativeCall35 |

Inclusive normalization generator/iteration557, enumerable transport486 and
text/storage318 overlap useful work. Call/frame selection356 includes useful
operations too. Calls dispatch927 and call/frame selection462 overlap; these are
not removable shares, cannot be added, and predict no speedup. Exact symbol lists,
all thread CPU/samples, malformed-map conditions and unknowns are retained.
An initial group matcher missed StartInline/StartNativeCall and misleadingly counted
zero call/binding matches; corrected symbol groups and the original analysis are
both archived. No sampling observations changed.

Eight guest seconds warmup per job;199Hz perf CPU-clock/frame-pointer with diagnostic
perf-map override. Same quiet F4as_v7/Ubuntu24.04.4, four EPYC9V45vCPUs,16GiB/no
swap,.NET11RC3 natural workstation GC/normal tiering. Active-worker CPU4.77/5.08s;
background GC.15/0,JIT.04/.03. Includes mediated RAM I/O, reset/protocol/goldens
and prior-result cleanup; wider than guest clocks. Not compilation/startup,
CPython comparison,RSS,pending-I/O or embedding-tail evidence. Inlining limits
precise attribution and this does not isolate the source of R448's call regression.

**Selected design direction:** retire language iteration through CheckedSequence,
CheckedCollection, AsyncSequence and CLR-yield bridges onto the existing native
cursor/result protocol. Textual inventory finds223 mentions across66files,
including definitions; it is not a resolved call count. Genuine CLR storage/view
interop remains at explicit boundaries. R438's materialization-only attempt added
four wrappers/per-item append work, left the adapters alive and measured slower.
Do not replay that shape. A retained migration must remove the displaced routers
and orphaned adapters, without a second iterator API or a new cleanup controller.
Start with complete leased materialization, then native bulk/language consumers;
keep the migration private until the complete deletion milestone is reviewable.

Preserve immediate acquisition/effect order without refusal, ChainMap snapshots,
known counts/self-extension, live mutation, None/identity, full join consumption
before type validation, user callbacks, genuine suspension and iterator-slot
precedence. Keep first refusal terminal, current-token loops and pending disposal
funded/joined. Conservative earlier admission may be proposed explicitly; do not
silently change acquisition or cleanup contracts. No heap tracing or new policy.
Use focused existing tests; one bounded whole normalization plus numeric/report,
CSV and call control pair only once a coherent displaced stage exists. Warm/fresh/
compilation separate; allocation is distinct. Refine/retain/remove from evidence,
with call-loop debt and moving controls explicit. No fixed percent veto.

Call/frame control remains a separate larger choice: R447 proves managed span/ref-
state expression APIs but not a whole-script JIT benefit. First-use and compile-
once latency, ownership/cancellation and exact no-replay transfer remain gates.
Do not infer a JIT gain or remove stack admission from these profiles. Existing
strip-character scratch is already admitted in CreateStripMethod before its
HashSet helper; inspecting that helper alone would falsely report an accounting
gap. No new memory audit or redundant reservation is selected.

All image/golden/quiet/Closed/exit/join/archive gates pass. RAM traces verified
persistently under lease before unmount; no users/fixtures remain and terminal is
cached forever. Only B849/B850 working raw JSON copies totaling696,549B were
retired after equality to current committed archives plus fresh UID/path/hash/
all-inode-alias proof. Selected images, canonical evidence and254/252MiB floors
remain. Publication/tagging held; regex remains independent.

[All attribution](attribution.json), [consumer inventory](source-consumers.json),
[exact image](identity.json), [raw capture](observations.tgz),
[procedures/original analysis/custody](procedures.tar.xz).
