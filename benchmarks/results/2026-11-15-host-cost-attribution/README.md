# Complete file-search host boundary

R401/B786, 2026-11-15, runtime **51e3c2c1 / frozen22e5b72c**. The existing
protected file-search script reads 2,049 lines / 42,010 UTF-8 bytes, stops on
line1,025, writes the match and prints the count. No runtime change or new
CPython timing. This closes the host question from the
[CPU capture](../2026-11-15-search-cost-attribution/README.md).

One process uses the same compiled script and explicit shared budget, with the
original comparison host, a forwarding/counting-only host and the same forwarding
host with operation timers. Each role warms for five complete-request seconds;
five rotating rounds then run256 full jobs/role. Original paths/Cwd, capabilities,
tokens, budgets, positional streams and synchronous-completion markers are
forwarded. Unexpected suspension is awaited rather than abandoned. All measured
host operations complete synchronously; this is not async latency qualification.

| Role | Median public Run µs | Result disposal µs | Complete job µs |
| --- | ---: | ---: | ---: |
| Original host |261.775|0.070|333.035|
| Forwarding/counting only |262.981|0.073|333.849|
| Forwarding/counting/timers |263.067|0.072|334.471|

Public Run excludes fixture reset, golden checks and result disposal. Complete
job includes those costs; wider process CPU also includes them. Startup,
compilation and final compiled-owner teardown are outside these clocks. These
independent medians are not additive. No pure-interpreter CPU is derived by
subtracting host wall spans.

Timed host spans are **about36.5µs/run,13.9% of instrumented Run wall time**.
Two opens account for27.95µs, two reads2.84µs, two disposals2.66µs and one
write2.16µs. Position/length/Cwd/flush make up the remainder. Observed forwarding
perturbation is+0.46%, and forwarding plus timers+0.49%, against the original
host in this process. These controls expose instrumentation cost; they are not
portable confidence bounds or a production speedup. Host opens dominate the
host portion but do not explain the whole published Python gap. Result cleanup
is not a dominant cost here. The CPython host has different authorization and
admission responsibilities; that comparison caveat remains.

Source checks additionally establish that shared admission already uses one gate,
ordinary and native dispatch already read compact instructions by reference,
default string affix matching already uses UTF-8 spans without a scalar-count
pass, and immutable builtin definitions/signatures are already shared. There is
no duplicate lock, instruction-copy pass or missing affix conversion to remove.
Complete call activation still includes real binding/stack/cleanup work. The
[withdrawn entry reroute](../2026-11-15-unified-helper-entry-withdrawn/README.md)
already tried routing those calls through existing funded storage and measured
slower; repeating it under a new label is not a new design.

The quiet F4as_v7/Ubuntu24.04.4/four EPYC9V45/.NET11RC3 collection completes in
20.126s. Full output/file goldens, per-invocation compiled-owner balance, final
zero budget, full-image/catalog identity and quiet/join/cleanup gates pass.
Persistent evidence verifies under the lease before RAM fixtures are removed.
No users or fixtures remain. Local Windows observations are retained separately
and have much larger open costs; they are diagnostic, not VM observations.
An initial compiler capture warning is corrected before measurement; original
and final build logs both survive. No timing trial is discarded.

Close this bounded diagnostic without selecting a patch from inclusive sample
names. Remaining complete activation/dispatch costs need a materially different
replacement and semantic acceptance; they are not established irreducible costs.
The whole-script roadmap remains open. No additional qualification of the
unchanged production runtime is needed for this private observer.

[Reproducible summary](summary.json), [complete image identity](identity.json),
[committed source map](source-map.json),
[all original VM samples](observations.tgz), [cleanup receipt](archive-receipt.json),
[source, local controls and procedure](procedure.tar.xz).
