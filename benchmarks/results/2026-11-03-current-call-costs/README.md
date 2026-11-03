# Current whole-script call costs — B159 source gate

2026-11-03. Diagnostic CPU sampling of exact B158 runtime source b3f1e9c3,
core5b4c4e6e2411c94eb8b42cb612996a419490b336830b6c7169ebafdd713868a5.
No runtime prototype or performance claim. Reuse the existing checked comparison
worker and perf collector, with six seconds warmup and2048 complete invocations
per script. Both use actual RAM-backed file APIs, ordinary GC/tiering and the
same full-output checks as B158. DOTNET_PerfMapEnabled=1 is diagnostic. Capture
7.18s text /11.95s report; active guest CPU7.12/11.18s. Do not treat captured
elapsed as an uninstrumented benchmark or compare it to CPython.

| Named leaf samples | Text /1417 | Report /2223 |
| --- | ---: | ---: |
| Interpreter ExecuteUntilCall |237|202|
| Positional binding |11|16|
| Bound-storage rent |3|5|
| Bound-values return |6|1|
| Monitor enter/exit |99|234|
| Native return guard/evaluation |37 /32|0 /0|
| Unknown |86|350|

The binding/rent/return names together cover1.41/0.99% of recorded guest samples.
Inlining and unknown/native frames prevent interpreting this as an upper bound
or exact causal cost. Inclusive stacks overlap and must not be added as CPU.
Text also has ordinary-call dispatch72 samples and interface/type tests; report
spreads work across JSON decoding/keys, full activation, formatting/parsing,
file staging and reservation locks. No single narrow mechanism explains its gap.

Source still shows a redundant bound-values copy before already eligible native
module helper returns. A span-based shared evaluator could eliminate that storage
without a new arena. However, this evidence does not establish a dominant CPU
cost. Do not automatically promote that isolated prototype as the next substantial
performance milestone. The proposal remains available for coherent activation
simplification; it is neither a failed design nor permanently closed.

The next design gate must consider complete function activation and prepared
formatting together: call context/local/operand/cache construction, native guard/
evaluation and repeated literal-spec parsing/temporary metadata. Preserve the
single executable engine, general binder for true dynamic cases, Python defaults/
closures/locals/error order, actual suspension, cancellation and ownership. State
the displaced routes and a complete implementation milestone before coding.
No new sampling campaign is required to confirm that binding alone is unproven.

Both workers verify before/after, close and exit0; fixtures are cleaned. Machine
gates pass before/after; the owned service exits0 in41.385s. Unit inactive/dead,
shared lease independently free. No runtime code, quotas or native probes added.
Public records preserve mapped raw chains/perf maps and all unknown frames;
original perf.data remains on the VM.

[Observations](observations.tgz), [assessment](assessment.json),
[identity](identity.json), [original source proposal](source-gate.md).

The bounded source pass also confirms that function namespaces are already lazy
and empty nonlocal tables shared. Those are completed optimizations, not proposed
new gains. Full activation still couples lexical context with active interpreter
state; f-string literal format specifications are parsed into fresh runtime
metadata. Any consolidation must preserve escaping lexical contexts and discard
private execution buffers before returning their funding. A blind context/frame
merge or a second formatting evaluator is not justified by these samples.
