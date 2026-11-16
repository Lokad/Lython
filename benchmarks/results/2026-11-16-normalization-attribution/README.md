# Complete normalization CPU attribution

R428/B824, 2026-11-16 UTC; committed **0a04e253 / measured810dafdd**. Same
52,224-byte/4,096-line script as the [CPython comparison](../2026-11-16-normalization-cpython-checkpoint/README.md).
597 source/build/test inputs verified; production unchanged by test-only corrections.
Full43800910729 is pending. No source comparison or performance gain measured.

Eight guest seconds warmup; **4.841 guest seconds /3,840 complete invocations**
sampled at199Hz CPU-clock/frame-pointer, diagnostic DOTNET_PerfMapEnabled=3.
Whole worker includes mediated RAM I/O, resets/protocol/goldens and prior-result
cleanup, wider than execution clocks. Quiet F4as_v7/Ubuntu24.04.4, four EPYC9V45
vCPUs,16GiB/no swap,.NET11RC3 ordinary tiering/natural workstation GC.

981 active-thread samples,193 unknown leaves; execution worker4.62 CPU seconds,
GC0.33,JIT0.08. Other-thread CPU/raw samples retained. Inclusive overlapping
groups:521 iteration/drain,399 generator lifecycle,261 bulk text,192 admission.
They include useful/inlined work and are not removable overhead shares. Execution
dispatch/call groups overlap most guest work and cannot justify replacing the ABI.
Nearest frames include75 instruction prefix,57 Reserve,44 rstrip,31 generator
advance and21 drain. Managed reflection resolves the94-nearest/78-leaf anonymous
method as **ExpandTabsMethod** (7.95% of active leaves); splitlines and TextWrite
anonymous methods are also identified. No inference that all text/call/GC work
can be eliminated.

Select **R429 direct bulk-output construction**: expandtabs currently decodes,
checks and appends each scalar; join has already materialized private input but
adds another iterator and growing byte builder. Replace both output-construction
pipelines with measured native UTF8, existing one final admitted PyString storage
and bounded copies/fills. Remove their growing-builder representation and join
iterator/helper; preserve arbitrary iterable draining, error order, native string
coercion, Unicode columns/newline resets, cancellation and funded inputs/output.
This is a bounded coherent storage milestone, not a prediction that it closes the
whole gap. Generator/frame construction remains a later independent source gate.
Measure complete normalization warm/fresh, report and unchanged JSON controls;
allocation/CPU/elapsed decisions stay separate and all adverse conditions remain.

Original capture worker verified both goldens, Closed and exit0. A preparation
replacement accidentally changed `perf report` to `perf normalization`; service
exit1 and original failed report are preserved. Recovered only offline processing
of the same immutable perf.data: no guest or capture reexecution. Fresh quiet
gate and persistent archive/hash verification completed under lease before RAM
unmount; all users joined. Two staging failures (missing old working archive and
disk headroom) also retained; both stopped before mutation/capture. Only exact
Git-archived VM replicas were retired after path/hash/ownership/all-alias checks;
current core images/toolchains and254/252MiB floors preserved. No trace abandoned.
The failed service terminal remains failed and cached; the recovery is explicit.

[All samples](attribution.json), [method identities](lambda-identities.json),
[raw capture/recovery](observations.tgz), [procedures/original conditions](procedures-and-original-conditions.tar.xz).
