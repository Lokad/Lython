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
