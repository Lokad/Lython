**B52 — complete numeric-report attribution and design decision.** Existing CSV/text
profiles do not establish current boxed-value/bulk-accumulator costs. The maintained
integer/float numeric-report scripts load two arrays from JSON, sum them and write
a complete JSON report; these cover a distinct report-generation workload. Inspect
the actual current aggregate/operator/parser paths before proposing changes.

Source before capture: SumState already keeps native integer/float accumulation
in scalar fields with exact overflow promotion. JSON defaults already parse short
integers/floats directly from UTF-8; only large integers retain text fallback.
Array/object parsing still awaits the shared value parser for each child, including
synchronous calls. These facts prevent obsolete boxing/conversion proposals but do
not establish which remaining work dominates. Current restored core6e51564b is
fresh-built at informational20e208c0; production source at5f3338a8 equals95b5955c.

Hypothesis to test, not a finding: generic value/call transport or repeated scalar
representation work materially dominates these complete jobs. Alternatives include
JSON decoding, I/O/cleanup and required arithmetic. Removing objects or seeing a
large wrapper method alone does not establish removable CPU. B51 already shows
that deleting the async opcode state machine is insufficient for a material gain.
Do not reopen it or tune a local comparator/binder/scan/reservation shortcut.

One fixed source/attribution decision, using existing evidence and checked profiling
tools. If current source leaves attribution unresolved, allow ONE paired capture:
`scripts.numeric-report.integer.large` and `.float.large`,512 warm synchronous
complete jobs each,199Hz cpu-clock/fp/perf maps, ordinary GC/tiering. <=12s combined
capture/45s owned collection. Actual named guest thread, all other thread deltas,
unknown/inlined/malformed-map limits; quiet/full stdout/file goldens, current source
identity and joined worker/profiler cleanup. No recapture if sparse/inconclusive.
Compilation/startup excluded; I/O/validation/disposal included and never inferred
by subtracting phase medians. Existing B28 fresh/compile boundaries remain separate.
No new benchmark framework or open-ended qualification campaign.

Stop after one source/attribution decision. Select a coherent deletion of dominant
transport, conversion or repeated state construction, rather than another sequence
of marginal local tweaks. If no substantial redesign is justified, record the
specific limitation; do not force code. Failed experiments never stop independent
correctness work or new producer integration.

Before any next candidate, record its exact hypothesis/deletions, affected complete
scripts, semantic/lifetime acceptance and intentional tradeoffs. Freeze ONE short
before/candidate/identical-before comparison with an unaffected workload, material
CPU AND elapsed criteria, control drift/CPU-resolution/quiet/golden/balance gates,
same runtime/tiering/auxiliaries and a hard collection bound. Preserve every adverse
or inconclusive result; no favorable retiming, phase subtraction or allocation-only
speedup. Use current sources and fresh nonincremental equal binary copies.

Semantic acceptance must preserve Python values/errors/evaluation order, defaults,
closures/generators and genuine protocol/host suspension, managed stack protection,
effective cancellation, first refusal before mutation, retained aliases and funding
through actual users and awaited cleanup. Focused Release first; full Windows/Ubuntu,
original stress and Release package consumers only for retained code at a coherent
delivery boundary. Do not rerun unchanged terminal qualification.
