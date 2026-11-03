# Config file boundary — B155

2026-11-03. One corrected bounded syscall diagnostic, not a speed comparison.
Exact B154 source images578496bb baseline/c53e91be candidate plus CPython3.13.16,
same config source/fixtures, ordinary profiles. Ten verified complete warm jobs
per image under strace, including staged resets. No new runtime code or dependency.

Both .NET images open config with O_CREAT and make separate ftruncate(fd,0)
calls. CPython opens with O_CREAT|O_TRUNC and makes no separate ftruncate call.
The ten guest .NET truncations can be slow; reset truncations are separate.
The collector's known reset-then-guest ordering classifies20 config write opens;
[raw lines and phase classification](syscall-assessment.json) remain inspectable.
Tracing perturbs syscall durations: do not use their sums or wall-minus-guest-CPU
to quantify the untraced latency gap. This observed host/filesystem asymmetry
limits attributing the B152 config6.29×fresh/8.18×warm ratio to interpreter CPU.
It does not prove all of that gap is I/O or establish a runtime speedup.

All full stdout/file goldens before/after, identities, quiet gates, normal worker/
tracer joins and owned fixture cleanup PASS. Corrected service6.115s/exit0, lease
free. The first controller wrongly required exit0 after deliberately interrupting
strace. Its2.266s failure, original controller/partial trace/gate remain. Corrected
controller accepts expected SIGINT(-2), verifies remaining worker operations and
retains failure state. This measurement correction repeats tiny observations,
not a throughput campaign or erased adverse result.

Next bounded check: retain ordinary filesystem results, compare only affected
config/copy warm/fresh cases on a shared RAM-backed fixture directory under the
same workers/engines. Existing TMPDIR mechanism, fully mediated file operations,
identical source/output/cleanup and unchanged-source controls; no host API change,
native production code or silent replacement of published ratios. This separates
storage sensitivity from another speculative CPU redesign. No predicted speedup.
