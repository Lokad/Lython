# B80 current compilation cost pulse

Source gate: old text-compile allocation types specific to Lowered nodes total
1,279,200/181,865,960approxBytes (0.70%); not full lowering cost or a current CPU
share. Real lowered consumers remain. The former dominant ordinary expression
iterator has become a struct traversal; old origins cannot select today's rewrite.
Function/lambda/comprehension facts are partly prepared/cached already.

Freeze current delivered4cae38f4 runtime/ordinary0.4 adapter; source checkoutf9fc488d.
One unchanged maintained text-summary-helpers source, compile-only32-job requests
until2s monotonic deadline, at most3s collection work.128 compilation warmups and
2s settling. Existing EventPipe collector readiness handshake and allocation
stacks, actual compile caller stacks across all threads (not maxCPU heuristic).
One24s owned VM service with shared lease, normal GC/tiering, both quiet gates.
No alternative code/runtime or public options, no forced collections/GC override.

Verify full CPython/Lython stdout/file goldens before/after. Compile requests must
succeed with no guest output; final compiled/result owners return allowance0;
actual worker/tracer joined and fixtures removed. Preserve missing stack/type
information, all thread counters, collector overhead and natural GC effects.
No latency/CPU-share/retained-memory inference from allocation ticks.

Use existing TraceEvent tooling offline, no framework or dependency in core.
Identify all actual compiler threads from resolved Compile/CompileCore frames,
not their names or signature type references. Record unresolved events separately.
If any collection gate fails, preserve and stop without retry/retiming. If current
costs are minor/distributed or source-required, close without runtime rewrite.
Any selected substantial mechanism needs separate frozen semantic, cancellation,
admission, deletion and short before/candidate/control gates before code changes.
No B68 small-config recapture or unchanged CI/package/adapter qualification.
