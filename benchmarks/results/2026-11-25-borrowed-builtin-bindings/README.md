# B1715 Borrow final builtin argument slots

The candidate shares signature validation and final placement between
ordinary owned binding and a managed four-slot caller buffer. Forty-seven
fixed-width synchronous math callables use that buffer from ordinary
executable calls on both hosting APIs. Their bodies accept a read-only
span. Dynamic callables, variable-width signatures and operations that
can suspend retain their existing owned binding. The sole delegate
base is folded into its builtin subclass.

The baseline is `e4cc71d6e`, the interim direct binding change.
Both frozen 32-file images differ only in the core DLL and verify
six complete CPython 3.13.2 goldens. The Azure F4as_v7 ran Ubuntu
24.04.4 and .NET 11 RC3 on November 25, 2026 UTC. Two workers per
image warm for five full-request seconds and each contribute seven
rotated timed batches. Ranges span all four worker median pairings.
Warm elapsed includes applicable mediated RAM-backed I/O, output
and file verification and result cleanup. Wider CPU includes fixture
reset and request framing.

| Workload and warm phase | Elapsed change | Wider CPU change | Elapsed worker spread reference / candidate |
| --- | ---: | ---: | ---: |
| Three-argument tolerance report | −15.54 to −10.72% | −14.48 to −9.12% | 3.18 / 2.45% |
| Two-option JSON export | −0.63 to +1.87% | 0.00 to +0.88% | 2.08 / 0.43% |
| Four-argument tolerance report | −13.10 to −8.46% | −14.19 to −8.52% | 4.12 / 1.17% |
| Default JSON export | −2.67 to +0.38% | −2.22 to +0.18% | 2.38 / 0.73% |
| Three-argument tolerance report, async | −13.38 to −9.08% | −13.55 to −8.75% | 1.64 / 3.27% |
| Wide-option JSON export | −0.55 to −0.16% | −0.91 to +0.92% | 0.00 / 0.39% |
| Ordinary definitions | −2.50 to +0.20% | −2.34 to +0.54% | 2.71 / 0.05% |

Both tolerance reports allocate 229,376 B less over 4,096 calls in the
local sync diagnostic, exactly 56 B per call. JSON and definition allocation
and peaks are effectively unchanged. The measured wider CPU reduction
describes the comparison to B1714b. The separate check against the earlier short-call
runtime leaves sync timing inconclusive and shows a small async CPU
reduction. Retain the shared binder and stack storage.
[Prior-runtime check](prior-runtime/README.md).

Windows .NET 11 and Ubuntu .NET 10 each pass 1,276 white-box and 2,952
public focused Release checks. Binding diagnostics, optional options,
aliases, genuine async JSON callbacks, before-body refusal and allowance
reuse are covered. All 196 timed batches and both quiet gates pass.
Workers join, guest fixtures are removed and the archive verifies under
the lease.

[Interim source](../2026-11-25-unified-builtin-operands/fixed-reservation/README.md) ·
[Identity](identity.json) · [Timing](quiet-summary.json) · [Allocation](allocation-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Terminal receipt](quiet-terminal-status.json) ·
[Procedures](procedures.tar.xz).
