# Current report/CSV CPU source gate — B134

2026-11-02, exactfd7bc7e4/core547825f9 after compiled operand layout and linear
execution. Read-only attribution, not a timing comparison or speedup. Existing
perf cpu-clock199Hz/frame-pointer protocol, ordinary GC/tiering, diagnostic
perf-map override only; dedicated quiet F4as_v7 Ubuntu24.04.4/.NET11RC3.

| Complete warm capture | Jobs | Wall s | Guest CPU s / samples | Unknown leaves |
| --- | ---: | ---: | ---: | ---: |
| Report rendering |1,024|8.781|6.34 /1,258|22.8%|
| CSV helpers |1,024|7.238|5.50 /1,094|5.3%|

Actual named execution-thread attribution, complete before/after stdout/file
goldens and loaded identities, normal workers/profilers joined. Both quiet gates
PASS; owned service27.950s/exit0/inactive, owned fixtures removed, VM lease free.
All other-thread deltas remain in records. These are on-CPU diagnostic samples,
not an elapsed decomposition; do not subtract CPU from wall to attribute I/O.
Inlining/native transitions and malformed final map records remain explicit.

Report: interpreter leaf76/1258 (6.0%), Monitor enter/exit147 (11.7%), JSON string
scan46 (3.7%), native UTF-8 string copying35 (2.8%); JSON value/object/array inclusive
chains are substantial. Unknown report leaves are too large for precise attribution.
Formatting and its useful output work remain visible. Namespace dictionaries are
already lazy; no dominant separate context-constructor cost is demonstrated.

CSV: interpreter112/1094 (10.2%); DictReader.NextAsync inclusive436 (39.9%),
record source274 (25.0%), parser Feed203 (18.6%). These overlap and include useful
parsing. List storage construction26 leaf samples (2.4%), FinishField27 (2.5%),
Monitor76 (6.9%). Native return guard/evaluation26 each includes real helper work;
no exact removable calling/boxing/ownership fraction follows. Required allowance
locks must not simply be removed.

Source inspection selects a neutral row-construction simplification: parser
currently stores cells in a governed reusable List<object>, then copies them into
a separate governed PyList before publishing. Build the final ordinary PyList
under existing capacity admission instead; detach it at record publication and
retire private staging capacity/growth/trim accounting. Use existing unpublished
rollback for partial rows. Header/dictionary construction remains outside parsing;
this does not restore the slower B110 parse-time dictionary fusion.

Hypothesis: remove one row representation/copy and its lifetime policy across
ordinary csv.reader and DictReader. Not a predicted speedup; changed early admission
and all parse/error/alias/cancellation/queued-row semantics must be checked, then
one controlled complete CSV comparison with text/JSON and variable-row controls.
Retain adverse data and decide the tradeoff before integration. Broader call/scalar/
text work stays open; no new sampling campaign or tiny builtin tweaks follow.

[Summary](summary.json), readable collectors, [plan](pre-capture-plan.md) and
[complete raw events/maps/chains](raw-capture.tar.gz) preserve all evidence.
Privileged read includes root-owned perf.data; only live control/ack FIFOs are
excluded from the archive. This external profiler adds no production dependency
or native/unsafe code. Full release qualification remains independent.
