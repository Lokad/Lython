# Complete invoice call/operand attribution

2026-11-15. R404/B789 compares fully qualified **51e3c2c1 /22e5b72c** with
withdrawn **17b04146**. One existing invoice script, eight guest seconds warmup
and one five-second199Hz CPU-clock/frame-pointer capture per source. Same
worker/catalog/goldens and quiet F4as_v7 Ubuntu24.04.4/.NET11RC3 host. Perf maps
are an explicit diagnostic override. This is not a controlled elapsed comparison.

| Diagnostic | Qualified22e | Withdrawn17b |
| --- | ---: | ---: |
| Complete invocations |736|576|
| Guest-thread CPU/invocation |6.698ms|8.819ms|
| Other-thread CPU/invocation |0.122ms|0.278ms|
| Guest / other samples |979 /17|1009 /27|
| Unresolved guest leaves |67|67|
| Ambiguous guest leaves |0|0|
| Binding / workspace / namespace-frame inclusive samples |0 /0 /0|21 /11 /0|

Inclusive samples overlap useful work. Zero observed samples do not prove zero
cost. They do not support making frame/loan caches the primary redesign.
Resolved leaves instead include native loop105, ordinary prefix101 and generic
input handler44 in22e; common prefix145, generic input84 and local completion28
in17b. The latter also has construction/binding/completion work. Exact causal
shares remain unresolved. A loop containing value operations is not pure dispatch
overhead, and none of these counts predicts a removable fraction or speedup.

Close attribution here. The selected R405 experiment separates completed builtin
arithmetic from rich generic async operand handling and shares arithmetic/operand
semantics across existing execution forms. It must remove displaced selectors and
probes, preserve actual protocols/cleanup/ownership and earn its own whole-script
comparison. It is not delivered or performance-qualified by this capture. Existing
immutable line windows, native decimal parsing and bulk CSV scans are already
present; this assessment does not rediscover them or reopen memory accounting.

The preceding Windows code-generation diagnostic uses exact frozen **45dd2da1**
(qualified6e276759) versus17b, separately from Linux22e. IL prefix sizes3004/3135B
and optimized native-loop/common-loop sizes10626/10305B do not support a simple
larger-loop explanation. Different shapes/PGO/EH prevent CPU attribution from code
size. The diagnostic warms one complete invoice case for three full-request
seconds/source with explicit JitDisasm/JitStdOutFile overrides, ordinary tiering/GC,
full goldens, Closed/exit/join and owned-directory cleanup. No timing or Linux-code
claim follows. Its original overly strict post-Closed fixture assertion and dump
remain; the worker had joined and its owned directory was still cleaned up.

Sampling covers the whole batch worker, including fixture reset/protocol/full
validation outside execution clocks. Per-thread CPU and every raw sample remain.
It measures neither pending I/O, heap/RSS, contention nor embedding latency tails.
All VM identity/golden/quiet/ownership/Closed/exit/join gates pass. RAM traces/maps
are archived and hash-verified persistently under the lease before teardown.
No users or RAM fixtures remain; B789 is terminal cached and must not be requeried.
Initial staging path and space preflight failures occur before mutation and stay
preserved. Only exact committed-archive working duplicates are retired after
path/link/UID/hash proof. Qualified images, canonical evidence and floors stay.

Raw identity metadata inherited stale descriptive R397 role/commit labels;
actual core hashes and loaded auxiliaries were checked independently and match
the sources above. The linked human-readable identity corrects those annotations;
the original remains in the immutable observations/procedure archive.

[Every resolved/unresolved leaf and overlapping category](attribution.json),
[actual source mapping](identity.json), [decision](decision.json),
[raw traces/requests/maps](observations.tgz),
[original procedures, metadata, JIT diagnostics and failed conditions](procedure-and-codegen.tar.xz).
