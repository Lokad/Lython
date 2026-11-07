# Complete report execution attribution — R52/B410

2026-11-07, qualified production55e510ff/frozen83af401e, unchanged worker and
optional 0.4 adapter. Dedicated F4as_v7/Ubuntu24.04.4/.NET11RC3. One maintained
JSON-to-Markdown script; six guest-seconds warmup, two-second settle and six-second
wall 199 Hz CPU-clock/frame-pointer capture. Job clocks4.94475s, actual named
guest-worker CPU4.67s, **927 samples / 59 unknown leaves**. All raw threads, stacks,
maps and inlining limits remain. This is CPU attribution with declared PerfMap3
and five JIT listings, not normal-runtime elapsed or a source-pair speedup.

| Sampled work | Self | Inclusive / interpretation |
| --- | ---: | --- |
| Executable instruction prefix |109|830; includes useful inlined work, not removable dispatch share |
| JSON string reading |66|Useful scanning, ownership and construction |
| Formatted instruction handling |42|Direct append already exists |
| Append formatted value |42|Formatting, validation and routing |
| JSON container remainder |39|Useful decoding/construction |
| Text file write |37|Mediated I/O and character accounting |
| Monitor exit / enter |35 / 19|Allocation admission dominates observed lock callers |
| Positional function binding |13|Not evidence for dominant argument/frame repackaging |
| Rent function argument storage |3|Existing borrowed operands/final slots/workspace reuse remain |
| Advance call frames |3|Large inclusive totals include callee work |

Inclusive stacks overlap; do not add them or predict saved CPU. Nineteen monitor
leaf samples pass through governed UTF-8 allocation/copy, with further record
creation and transferred-string admission chains. They do not establish that
necessary shared-budget synchronization can be deleted. Current source already
uses native/default JSON paths, shared record keys and direct final-buffer
formatting. A new call arena, universal tagged slots or second formatted evaluator
is not selected by this capture.

The source follow-up identifies a separate potential correctness issue:
MemoryGovernor publishes reserved counters only after host TryResize returns.
Reentrant same-governor allocation/refund during an admission callback may resize
the aggregate reservation from stale totals. A bounded native regression must
establish this before a correction; the profile alone is not proof. Preserve
admission refusal, retained aliases and cleanup rather than optimizing around it.

Both complete goldens, exact core/auxiliary/normal-runtime checks, quiet endpoints,
Closed/exit/join and empty fixture mount pass. Raw traces are persistent, separately
from a collection-owned tmpfs fixture mount, archived/verified under the lease;
the mount is removed and the archive independently verified locally.120s owner,
30s stop,30s observation margin. No new production code or repeated delivery check.
The inherited profiler docstring refers to text; actual workload/record/JIT targets
are report-render.large. No original record is relabeled.

[Raw archive](observations.tgz), [receipt](archive-receipt.json), [leaves](guest-cpu-leaves.json),
[all call chains](guest-call-chains.json), [classifier](analyze-chains.py),
[identity](identity.json) and [contract](contract.md) preserve evidence and limits.
