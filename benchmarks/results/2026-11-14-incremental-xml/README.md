# Incremental XML input and shared construction

2026-11-14UTC, R335committedcdd92944/frozenf4c4f027 against fully qualified
R334d888ee47/frozen0b2929ca. Same581candidate production inputs and fixed worker/
adapter bytes are recorded. Full R335platform/stress/installed qualification is
[GREEN43536449801](delivery/README.md): both platforms/actual11/10,
original stress and installed consumers;581inputs/16summaries cached.

`iterparse` supports start/end events, root and close over mediated filenames
or borrowed read objects. It replaces eager acquisition for early-exit jobs.
`fromstring` and events share one tree builder; the old inline build loop is
removed. Existing opened-file transport, protocol calls and execution cleanup
serve actual asynchronous reads and awaited disposal. No new resource registry,
memory policy, tracing, unsafe code or background work is introduced.

Private reader/buffer/construction storage retires at close. Before publication,
failures refund only unpublished values. Published prefixes keep command ownership;
their rollback ledger retires. Aliases remain funded. Reader/name/token estimates
can grow with supplied bytes until reader disposal, and graph charges remain
conservative. This is **early-exit capability, not constant-memory arbitrary scans**;
long streams still use bounded host executions. Partial-node traversal admits
remaining permitted depth, including after clear. Other event kinds/custom parsers
fail explicitly; the depth1024 and DTD/entity restrictions remain.

## Correctness

35internal/106public XML Release cases pass each actual.NET11/10, plus two
strengthened joined-cleanup cases per target. Four isolated CPython3.13.2 event/
root/close/error controls agree exactly. Existing Python encoding goldens are
reused through byte-fragmented readers, including declaration aliases/mismatches.
They expose and fix a prototype bug accepting UTF8 bytes declaring UTF16. Coverage
includes long tokens with genuinely asynchronous reads, retained results over two
executions/shared allowance, terminal refusal before I/O and inside read hooks,
cancelled reads with held cleanup, borrowed-source ownership, reentry, partial
trees/clear, malformed XML and depth. Frozen worker verification independently
checks full file/stdout goldens and accounting reuse for both script algorithms.

## One bounded complete-script comparison

| Complete script / phase | Reference ms | Candidate ms | Elapsed change | Wider CPU change | Process spread candidate/reference | Max IQR |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
|early-exit, changed algorithm /warm|24.8279–25.9720|0.0806–0.0812|-99.69..-99.67%|-98.74..-98.65%|0.68/4.61%|16.41%|
|early-exit, changed algorithm /compile-run|24.7566–26.1957|0.1329–0.1343|-99.49..-99.46%|-98.47..-98.47%|1.05/5.49%|22.26%|
|whole-document, identical source /warm|0.8813–0.8868|0.8501–0.8530|-4.13..-3.21%|-4.67..-2.35%|0.34/0.62%|1.45%|

B706finishes140.181s under one300s owner on quiet
AzureF4as_v7/Ubuntu24.04.4/.NET11RC3, ordinary workstation GC/tiering. Two fresh
processes per revision/job, eight-second full-request wall warmups and five
rotating0.5s wall-sized batches. The raw collection preserves all points.

The early-exit input is622,640bytes: one wanted item followed by16,384
unused records. Reference reads/parses the complete document then searches;
candidate uses iterparse and closes after the first wanted end event. Both print
`kept`, write the same result file and preserve the entire original input.
**Both runtime and script algorithm differ.** The large improvement demonstrates
avoided input/tree work on this shape, not a same-source interpreter or CPython
speedup. Reference processes vary4.61–5.49%; exact speedup magnitude is diagnostic.
The unchanged settings/report control supports a modest elapsed/CPU improvement
from shared construction; no broad runtime claim or allocation percentage follows.
Retain the usable capability and shared builder; no remaining measured CPU debt
is established by these three jobs. Unrelated text/search debt remains open.

Guest clocks include mediated RAM-file I/O, complete stdout/file validation and
prior-result cleanup. Compile-to-result adds compilation/compiled-owner disposal;
startup, fixture reset/protocol and final caches are excluded. Wider CPU includes
reset/protocol. Ranges pair process medians, not confidence intervals. There is no
new external CPython timing, allocation or embedding-tail measurement.

All identity/golden/balance/quiet/join/archive gates pass. Persistent evidence is
SHA-verified after all users join and RAM fixtures unmount. No evidence/image is
retired and no disk floor relaxed. The first missing diagnostic-helper preflight
and corrected fixed-worker dependency selection remain archived; no timing was
discarded or restarted. A duplicate launch request refuses the existing root;
the original unit alone completes. Publication/tagging remain postponed.

[Timing/wider CPU](summary.json), [raw observations](observations.tgz),
[frozen source/bits/checks and setup conditions](source-checks.tar.xz).
