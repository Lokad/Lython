# B92 report-format cost gate — before workload/code/capture

Question: does the remaining UTF-8 -> CLR string -> padded/truncated CLR string ->
UTF-8 pipeline dominate a complete report-rendering script? Existing numeric-report
mostly measures JSON and sums; it contains no record-wise formatted report output.
Do not infer a formatter redesign from its measurements or synthetic format loops.

Extend the maintained representative catalog with empty/small/large report-render
cases (0/48/3072 JSON records). Same plain Python under both engines, a small helper,
JSON input, Unicode names/path-like labels, string width/precision/centering and
integer grouping, Markdown file output and complete stdout/file goldens. Immutable
inputs/expected bytes are generated independently outside timing. Existing file
worker tests validate repeated fresh fixtures; local isolated CPython validates all
three scales. No runtime change, alternate host, policy or benchmark framework.

After focused Release checks, ONE <=3s native CPU capture of complete warm large
jobs on the existing quiet VM, existing profiler/driver/budget wrapper, normal
GC/tiering, actual named guest thread, full goldens before/after, final allowance0,
all users joined and normal fixture cleanup. External owned service <=30s, lease,
owned build servers stopped before gates; each batch32 jobs, no retry or retiming.
Report measured compilation/warm/I/O/cleanup only where directly recorded by
existing tooling; sampling excludes compilation/startup, includes mediated file
work/checks/declared cleanup and is not a latency decomposition or CPython ratio.

Choose a native string-format redesign only if formatter/conversion-rooted paths
are a substantial observed component (>=20% of actual guest sample chains, inclusive
once per sample; disclose unknown/inlined limits) and source confirms a deleted
representation boundary rather than already-native default formatting. Otherwise
close with the precise unresolved mechanism; no runtime prototype from this gate.

Candidate, IF selected later: native UTF-8 string width/precision/padding across
f-strings, str.format and format(), using existing parsing, governed builders,
current checks and owners. Keep numeric/date/custom __format__ hooks, error order,
Unicode scalar widths/multibyte fill, evaluation order and genuine suspension.
Before candidate code freeze its bounded before/after decision controls separately;
do not relax this cost gate or rerun a closed trial. This is not a replay of native
format-field parsing (Oct30), which already removed intermediate field/spec parsing.

Concrete collection sizing before any run: reflected/public phase diagnostics use32 warmups and8 observations per lane; profiler warmup32 jobs,2s settling,32-job batches until2s soft window, <=3s including the last complete batch. Hard whole-service30s. No startup/compile included in the CPU sample; current-thread phase allocations are not process-wide CPU fractions.
