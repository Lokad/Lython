# Precedence-parser prototype rejected (B46)

Source **9618a1dd + source.patch**, 2026-11-01. The prototype replaces six fixed
arithmetic/bitwise precedence descents with precedence climbing, deleting five
forwarding methods and 30 net parser lines. It preserves separate boolean,
comparison, unary and power paths. The current frontend
[profile](../2026-11-01-current-frontend-cpu/README.md) selected this experiment;
[criteria](pre-code-plan.md) and [counts](pre-measurement-counts.md) precede capture.

**Rejected and original parser restored.** Only configuration meets the required
>=10% lower compilation CPU against both identical-source controls; the target
requires two scripts. Fresh-configuration CPU control drift also exceeds 5%.
No tuning, retiming, relaxed acceptance or qualified speedup follows.

| Process CPU per job | Before | Candidate | Identical before | Candidate change |
| --- | ---: | ---: | ---: | --- |
| CSV compile | 86.538 us | 80.592 us | 87.226 us | -6.87% / -7.61% |
| Text compile | 66.454 us | 62.415 us | 66.939 us | -6.08% / -6.76% |
| Config compile | 57.530 us | 49.743 us | 57.209 us | -13.54% / -13.05% |
| Config fresh compile/run | 4.748 ms | 3.769 ms | 3.843 ms | Inconclusive: control drift -19.05% |
| Text warm run | 6.376 ms | 6.265 ms | 6.529 ms | -1.75% / -4.04%, diagnostic control |

Dedicated Ubuntu 24.04.4 Azure F4as_v7, four EPYC 9V45 vCPUs / 16 GiB, .NET 11
RC3, ordinary GC/tiering. One 78.98-second service, 15 ordered child processes,
below 90-second collection / 30-second child bounds. Compilation uses 512 warmups
and seven batches of 4,096 fresh compilations; execution controls use four warmups
and seven batches of 128 complete jobs. Compilation allocation is essentially
unchanged. Every CPU batch resolves the declared 100 ms minimum. Compilation
baseline CPU drift is below 0.8%; warm-text drift 2.39%.

Compile-only clocks cover public compilation, validity checks and disposal,
excluding startup/guest execution. Full output goldens run before/after those
samples. Fresh config includes compilation and disposal plus fixture reset,
mediated I/O, full goldens and result disposal; warm text excludes compilation.
Shared unbounded funding exercises ownership paths, with normal owner balances
checked throughout. Finite refusal is validated separately. Quiet gates, complete
goldens and cleanup pass; all children join, service inactive/dead/success, lease
available. No external-baseline or steady-state latency claim.

Fresh nonincremental built copies agree before freezing; auxiliary host/adapter
binaries are byte-identical across roles. Candidate core SHA256
`06ef9a8ae72f2895f231743580d2e926625d62e25ad47bae9d38b69a1b595c98`,
baseline `b85b07f5c2fd6e8f006f5221628b9e4b0a71f0d279f95fc2d5a4fde073f3e549`.
The [summary](summary.json) retains CPU/elapsed/allocation observations and flags
the failed control. [Raw results](raw-results.tar.gz), SHA256
`863bdd9180b44b2e8ed01430a12872230120dad7e67d06ecae248a75d546e69c`, preserve all
samples, exact patch, identities, source catalog, machine gates and adapted
existing driver. No benchmark framework or production dependency was added.

The prototype passes 713 focused Release checks on each actual .NET 11 RC3 /
.NET 10.0.8. Its 23 independent mixed-precedence/diagnostic/protocol-order cases
remain as semantic coverage. Restoration uses a fresh source mtime followed by
a nonincremental build; restored-source outcomes are recorded in PLAN.md.

Source review also finds that the ordinary parser receives its token but checks
it only through nested formatted-string tokenization. Ordinary parsing does not
observe in-flight cancellation. This existing limitation is a separate correctness
task; no cancellation improvement is attributed to the discarded prototype.
