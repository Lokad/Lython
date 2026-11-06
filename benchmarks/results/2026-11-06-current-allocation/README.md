# Current complete-script allocation origins — 2026-11-06

B345 completes two maintained scripts, 256 warm invocations each, in 22.63s:
**1.886s total capture**, ordinary GC/tiering, quiet F4as_v7/Ubuntu24.04.4/.NET11.
Complete stdout/file goldens, funding, Closed/join/fixture cleanup and quiet gates
pass. Raw RAM traces were archived/hash-verified persistently before lease release;
no worker/collector/trace fixture remains. Archive a552500b (full hash in receipt).

Source 88f12099 is fully qualified by terminal CI42208020544/all eight. This reuses
B343 core 1d1eff12 and unchanged b94 worker/protocol2: all modified source hashes
match; the core was built before the source commit with different SourceLink/
receipt metadata from installed package 908B7184. Actual Ready hashes are recorded.
Normal optional adapter 0.4 remains; private dev40 prototypes are not involved.
No new binaries/dependencies or production instrumentation were transferred.

| Complete script | Guest allocation ticks | Approximate guest bytes | Leading sampled types |
| --- | ---: | ---: | --- |
| Text helpers,3,072 lines/6,144 words |4,144|441,832,352|PyString61.04%, byte[]15.00%, object[]11.56%, BigInteger7.84%|
| CSV functions,4,096 nonblank records |13,035|1,389,658,576|ExecutionContext31.74%, PyString14.24%, native entries14.14%, BigInteger13.86%, byte[]7.35%|

The existing offline reader retains **all** allocation origins/types/thread totals,
with zero missing Lython stacks in both guest selections. An offline extension
reads the existing events' ObjectSize: sampled contexts are 424B, strings64B,
ordinary3-field native dictionary arrays184B. No new capture was used for sizes.

AllocationAmount64 credits sampled traffic to its triggering type. These are
**approximate attributed bytes**, not exact object counts/per-type shares, retained
storage, CPU fractions, RSS or uninstrumented timings. Repeated tiny allocation
patterns can bias attribution; do not turn text's61% into an exact opportunity.
Other threads remain visible. Compilation/startup are outside the warm workload;
mediated RAM I/O/full validation/prior-result cleanup remain included. No CPython
ratio or speedup is inferred from this diagnostic.

## Source/lifetime decision

CSV's ordinary `accumulate` needs a real lexical context today for its branch,
locals and mutation; text already uses the retained native-return optimization.
Argument/local backing is already shared and cached, namespaces/caches are lazy.
Do not invent an absent packaging phase or repeat the slower B61 caller records,
B69 arena, B64 string windows or B01 shared-key/cache proposals. Shared CSV keys
still require parallel mutation/query/iterator/promotion semantics; current
allocation evidence does not erase that engineering cost.

Select a bounded **context-on-demand** source gate: separate mutable executable
work state from lexical identity while sharing the existing instruction handlers.
Native operations may finish without constructing a lexical context; actual
scope/protocol/async work must materialize it and continue the same instruction
and owned locals, without restart or guest-effect replay. No context swapping,
pool, caller-record copies, slot arena, second interpreter, policy or new owner.
Preserve native-return/UTF8 formatting gains and managed admission/cancellation.
A source proof must establish safe operation/promotion boundaries before coding
that path; a mechanical work-state factor alone claims no improvement. Final
acceptance requires independent semantic/ownership checks and a controlled
complete CSV warm/fresh comparison with unchanged text/JSON/report controls.

B344 failed before Python/workers due to CRLF shell formatting. Original payload,
script and terminal log are preserved in failed-b344.tgz. B345 explicitly validates
LF/shell syntax and keeps all workload/measurement conditions unchanged. This
setup failure is not evidence against a runtime design. No favorable resampling.
