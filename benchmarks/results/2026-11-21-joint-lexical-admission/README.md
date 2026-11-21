# B1286 joint lexical definition admission

The prototype groups newly retained closure cells and capture backing with
the callable and its final annotation storage in one allowance request.
It commits the capture storage independently once the cells are available
to the parent. The still-private callable can release its own reservation
on failure. Namespace-dependent captures continue to use the existing
separate path.

Reference `256c5e651` and prototype `f1ace4677` ran November 21, 2026 UTC
on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. Two
processes per source warmed for at least eight seconds and contributed
seven rotated batches per lane. Ranges cover all four pairings of process
medians. Warm elapsed includes guest execution, captured stdout,
verification and prior-result disposal. Time to result also includes
compilation and code-owner disposal. These scripts do no guest file I/O.
Process CPU covers the full request. Startup and final teardown are separate.

| Complete script | Boundary | Elapsed change | CPU change | Local warm CLR allocation |
| --- | --- | ---: | ---: | ---: |
| Postponed nested factories | Warm | −4.33% to −1.67% | −4.18% to −2.37% | +0.00% |
| Postponed nested factories | Compile to result | −7.12% to −3.08% | −7.49% to −1.43% | — |
| Eager nested factories | Warm | −6.66% to +3.16% | −7.81% to +2.66% | +0.00% |
| Fully supplied helper | Warm | +1.36% to +3.67% | +1.47% to +4.78% | +0.00% |
| Eager rules | Warm | −1.93% to +1.31% | −2.54% to +3.37% | +0.00% |

Local counts show 10,265 allowance growths per nested factory invocation
on the reference and 8,217 on the prototype. Eager rules without new
captured cells remain at 6,169 and the fully supplied helper remains at
seven. CLR allocations and accounted peaks match on the three factory
cases. All seven locally checked cases return their allowance to baseline.

Both complete 32-file images verify against seven local goldens and
against the four goldens in the quiet catalog. Windows .NET 11 and
Ubuntu .NET 10 each pass 133 white-box and 587 public focused tests.

Keep the prototype private. The postponed factory gains agree across all pairings, but the fully supplied helper takes 1.36–3.67% longer and uses 1.47–4.78% more process CPU. It defines its callable once and takes the ordinary path for all 2,048 calls. Its allowance counts and allocations match, so the results do not identify the cause of the regression. Eager nested and eager rules elapsed span zero. A bounded confirmation with unchanged images could distinguish a recurring control cost from process variation. A redesigned ownership split should remove duplicate machinery before qualifying it for production.

All 140 timed batches passed. Workers joined, guest fixtures were removed
and persistent observations were verified under the lease.

[Timing](summary.json) · [Execution allocation](allocation-summary.json)
· [Reference admission counts](reference-growths.json)
· [Prototype admission counts](candidate-growths.json)
· [Raw observations](observations.tgz)
· [Source, tests and procedures](checks-and-procedures.tar.xz).
