# B1738 Combine buffered `DictReader` record admission

The candidate admits the final field backing and dictionary shell in one
request for a buffered line with a verified, unexposed inferred header. It
uses the parser's existing descriptors and emits the dictionary directly.
The completed row bypasses the caller's header lookup, two key-identity scans
and second reservation. An admission callback that exposes or replaces the
header leaves the raw row with the live-header path. Plain `csv.reader` and
ordinary staged, ragged or incomplete rows use their existing publication
routes.

| Workload | Phase | Elapsed change | Process CPU change | Same-source elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| File-fed CSV quantity map | Warm async | −12.31% to −3.90% | −9.53% to −3.76% | 7.23% / 2.21% |
| Shipment callback report | Warm async | −4.75% to −2.75% | −6.00% to −2.54% | 0.79% / 1.30% |
| Quoted policy invoice | Warm sync | −1.89% to −0.70% | −2.08% to −0.77% | 0.15% / 1.06% |
| Plain CSV reader | Warm sync | +0.76% to +10.53% | 0.00% to +11.11% | 0.25% / 9.42% |
| Unicode text report | Warm sync | −10.22% to −6.74% | −9.89% to −6.59% | 0.24% / 3.62% |

The reference is `0374493f9`. The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and
.NET 11 RC3 on November 25, 2026 UTC. Two processes per image and phase each
ran seven seconds of warmup and seven rotated batches. Negative percentages
mean the candidate uses less time. Ranges span all four pairings of process
medians. Warm elapsed includes mediated RAM-backed file reads and writes,
verification and disposal. Whole-request CPU also includes framing and reset.
The movement in both control workloads and the wide file-fed reference spread
limit attribution of this first timing block.

The separate route build produces 2,046 combined rows in both shipment and
file-fed map and 3,064 in the 4,096-row invoice, in each hosting mode. The
plain reader produces 4,097 ordinary arrays. Route runs return to their
compiled allowance baseline and then to zero. Measured images pass five
independent CPython goldens each. Retained and peak admission match in the
local diagnostics. CLR allocations differ by at most 672 B per invocation.

Windows .NET 11 and Ubuntu .NET 10 each pass 817 focused Release checks.
The new cases exercise mutation of an exposed header during admission,
reentrant raw pulls, cancellation, terminal refusal and retained row aliases.
All 140 timed batches and worker cleanup are verified.

[Confirmation](confirmation/README.md) · [Original-baseline check](original-baseline/README.md) ·
[Identity](identity.json) · [Routes](routes.json) ·
[Execution allocation](allocation-summary.json) · [Compilation allocation](compile-allocation-summary.json) ·
[Timing summary](quiet-summary.json) · [Raw observations](quiet-observations.tar.xz) ·
[Terminal receipt](quiet-terminal-status.json) · [Procedures](procedures.tar.xz).
