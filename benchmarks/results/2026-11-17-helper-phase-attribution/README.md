# Same-process helper workload phase attribution

R467/B873, 2026-11-17 UTC. Fully qualified ccf20ba3 / frozen 7a599347 and
CPython 3.13.16, unchanged eleven-case catalog, workers and golden checks.
This is a diagnostic of phase conditions, not a source change or replacement
for the independently warmed [published baseline](../2026-11-17-receiver-cpython-checkpoint/README.md).

The earlier helper-text result had CPython fresh 0.795–0.802 ms below warm
0.872–0.905 ms. Here both phases are warmed and alternated within each process:

| Complete script | Engine | Warm | Compile and execute once | Same-process fresh / warm |
| --- | --- | ---: | ---: | ---: |
| Helper-list normalization | Lython, two processes | 1.255–1.265 ms | 1.353–1.354 ms | 1.069–1.079× |
| Helper-list normalization | CPython, two processes | 0.715–0.720 ms | 0.833–0.840 ms | 1.157–1.174× |
| Helper-list report | Lython, two processes | 2.642–2.871 ms | 2.803–3.001 ms | 1.045–1.061× |
| Helper-list report | CPython, two processes | 2.203–2.253 ms | 2.355–2.396 ms | 1.045–1.088× |

The inversion does not recur. Text Lython warm/fresh remains close to the earlier
observations, while CPython warm changes substantially. Both text process controls
are close here; report Lython warm controls differ about 8.7%. External process
CPU includes reset/protocol, has coarse tick resolution, and supports the positive
text fresh/warm direction. Report CPU and controls remain mixed. Original B872
conditions are retained; this collection does not establish their causal origin,
resolve earlier normalization shifts or demonstrate a Lython performance gain.

Separate instrumented CPython processes reuse the worker with clocks around
compilation, invocation, guest exec, fixture reset and file validation, plus
ordinary GC callbacks. Text exec is about 0.674 ms warm / 0.692 ms fresh;
compilation about 0.089 ms fresh. Report exec is about 2.221 / 2.271 ms;
compilation about 0.113 ms fresh. Natural GC is observed in both phases. These
component clocks overlap GC and add overhead; they are not additive exclusive
costs or corrections to uninstrumented timings. No forced collections, GC disable,
runtime/tiering changes or cleanup omissions. Namespace/result cleanup remains in
the existing execution boundary; it is not isolated as a separate exclusive phase.

Source confirms eager Lython frontend/lowering/executable compilation and the same
runtime construction in both execution lanes. No lazy compiler, per-compiled-worker
startup or other removable Lython setup stage is demonstrated. **Decision: no
setup prototype or further unchanged phase retiming.** Advance current call/frame
and text transport attribution; this is not a permanent design ban or completion
of the whole-script performance objective.

Four jobs / 89.44 s, two unmodified processes per engine/case and one separately
instrumented CPython process per case. Each phase warmed for 3 s unmodified / 2 s
instrumented; five rotated approximately 0.25 s batches. Quiet existing Azure
F4as_v7 / Ubuntu 24.04.4 / .NET 11 RC3, natural workstation GC and normal tiering.
Warm/fresh boundaries and mediated RAM I/O/output verification/prior-result cleanup
are unchanged. Fixture reset/startup/protocol/final-cache teardown stay outside the
guest clock; external CPU is wider. Phases share process state here, unlike B872.

All identities, complete stdout/files, quiet-before/after, Closed/exit/join and
persistent archive checks pass. Every worker joined and RAM fixtures removed.
One exact committed B872 raw JSON replica (427,167 B) retired after fresh owner,
path, SHA, link-count and all-inode-alias proof; its canonical archive, current
images and toolchains remain. Staging/final 254/252 MiB floors preserved.
The first local wrapper preflight used the wrong required fixture prefix and was
explicitly rejected before VM launch; corrected preflight passes both scripts and
both phase frames. No production/test assertion or timeout was relaxed.

No source changes or repeated platform/package qualification. Publication held.
Read-only regex source update 85822987 changes only an internal PythonRe consumer
to Core byte-range fields; missing facade APIs and immutable dev105 bytes remain
unchanged. No adapter promotion or repeated producer qualification.

[Summary and all phase samples](summary.json), [raw observations](observations.tgz),
[frozen procedures](procedures.tar.xz), [identities](identity.json),
[archive custody](archive-receipt.json), [terminal](terminal-status.json),
[local preflight](local-protocol-preflight.json), [regex source status](regex-source-status.json).
Terminal state is cached permanently; no requery or unchanged replay.
