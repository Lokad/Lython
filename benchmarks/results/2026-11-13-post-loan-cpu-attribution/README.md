# CPU attribution after word-buffer consolidation

2026-11-13, B692: committed557d18d4/frozen9d157bb2, same complete text/search
scripts and quiet F4as_v7 VM as the earlier diagnostics. Fixed B685 workeracda3ef3
and optional0.4 adapter; all image hashes are recorded. This is a source gate,
not a before/after CPU comparison or an allocation profile.

| Complete script | Guest CPU samples / seconds | Unknown leaves | Leading resolved leaves |
| --- | ---: | ---: | --- |
| Captured text summary |575 /2.90|45 (7.83%)|Dictionary loop136 (23.65%); dispatch55 (9.57%); word preparation34 (5.91%); native runner33 (5.74%)|
| File search with keyword-only default helper |453 /2.30|93 (20.53%)|Dispatch49 (10.82%); native entry45 (9.93%); input instructions26 (5.74%); native runner24 (5.30%)|

One three-second cpu-clock capture at199Hz follows eight guest seconds of warmup
per script. Compilation is excluded. The worker performs mediated RAM-file I/O,
complete stdout/file validation and prior-result cleanup. Thread selection and
offline JIT-range resolution use the same existing analyzer. PerfMapEnabled=3 is
an explicit diagnostic override, not the normal timing environment. Wider worker
traffic, reset/protocol CPU and unknown kernel/CLR leaves remain in raw evidence.
There are no ambiguous leaf assignments. Owning frames include useful/inlined
work; their shares are not predicted speedups or removable overhead. Sample
movement from B686 is not evidence of a CPU improvement between revisions.

Quiet/image/golden gates pass; the owned120-second collection completes, users
join, and the raw archive is SHA-verified persistently before RAM fixtures unmount.
The first identity preflight accidentally selected the old reference core path;
it refused before staging. Correcting that path to the intended already-frozen
candidate is preserved with the original failed command and conditions.

Source inspection selects R326: the shared final-slot binder can consume flat
positional operands directly, including a helper with keyword-only defaults.
This removes the independent positional binder and its separate framed-entry
selection, without another cache, arena or workspace. Live defaults, protocol
lookups, receiver/argument error order and genuine suspension remain semantic
requirements. A separate controlled comparison decides CPU impact; no allocation
gain is inferred. Retain slower arena/control lessons and continue the broader
construction/transport roadmap after that decision.
