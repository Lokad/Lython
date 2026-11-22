# Final short-word publication in the Unicode report

The nonlocal Unicode report has a private local `word` and one prepared
histogram segment in the exact B1406 compiler image `55424591f`.
The inspected tape includes no captured `word` binding. One run through
the existing comparison host and a scratch instrumented copy counted
3,076 prepared loop entries and 3,072 normal exhaustions. At 2,047
of those exhaustions the implementation materialized the last short word
into an independently published object. The report has 3,072 lines and
6,144 words. It matches stdout and output-file goldens and returns the
allowance to its compilation baseline, then to zero.

The count isolates work that a compiler proof could remove. It does
not measure elapsed time, allocation after a replacement, or eligibility
in other workloads. The existing nonlocal width binding, dictionary
keys and total stay observable. An eventual proof must account for
forwarded operands, deleted names, exception and cleanup reads, captured
cells and namespace observation. Early interpreter fallback and unwind
still publish the last binding. Keep the eager split preflight and
cursor ownership. The counters exist only in the scratch image.

[Count and instrumented image hash](count.json) · [Source checks and fixture](procedures.tar.xz) ·
[Prior profile](../2026-11-22-retained-runtime-profile/README.md).

## Conservative prototype

Private `8189309f4` omits final-word publication on normal exhaustion
when its lexical binding has no reads anywhere outside the segment.
It keeps normal publication on fallback and unwind. The original
report reuses `word` in a later output loop. The compiler proof declines,
so its measured allocation stays at 446,144 B per invocation within
local measurement variation. For a control with a different output-loop
target and the same input/output digests, the compiler marks the segment.
Local CLR allocation falls from 446,160 B to 331,528 B (25.69%).
The admitted peak stays at 1,278,996 B. The one-worker allocation
diagnostic includes verified output and prior-result cleanup.

Keep the proof private. It demonstrates the benefit of omitting the
storage but misses the original workload. To apply it there, a proof
must establish that the later loop overwrites the local before reading
it, including zero iterations, exceptions, cleanup, and namespace
observation. Timing and integrated qualification remain open.

[Local allocation and image hashes](local-prototype.json) ·
[Prototype source and controls](prototype-evidence.tar.xz).
