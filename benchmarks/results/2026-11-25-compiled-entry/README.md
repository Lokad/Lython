# B1689 Direct compiled-script entry

The candidate stores the executable and its provider state directly on the
compiled script. This removes both runner delegates and their closure, and
constructs the runtime facade once per valid compilation. Each invocation
still has its own execution state and owners. Invalid scripts store their
diagnostic attribution, including refusal and cancellation.

The candidate is the archived production patch on `e4bbc3925`. On November 25,
2026 UTC, the quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11. The verified
worker images differ only in the core DLL. Four workers provide seven rotated
batches per phase. The ranges span all pairings of candidate and reference
worker medians. Negative values mean less time. Compile-only measures the
public compilation and disposal boundary. Warm execution reuses a compiled
script and includes applicable mediated RAM-backed I/O, output verification
and prior-result cleanup. Through result adds compilation and compiled-owner
disposal inside the running worker.

| Workload and boundary | Elapsed change | Wider process CPU change |
| --- | ---: | ---: |
| Short numeric command, compile | −1.01% to +0.63% | +0.00% to +0.00% |
| Short numeric command, warm | −0.74% to +0.49% | −3.11% to −0.98% |
| Short numeric command, through result | −0.65% to +1.36% | −0.18% to +2.53% |
| Defaulted rules, through result | −5.24% to −0.31% | −4.91% to −1.90% |
| Configuration manifest, compile | −0.79% to +0.90% | +0.57% to +3.47% |
| Configuration manifest, through result | −1.17% to +0.20% | −2.23% to −0.54% |
| Quoted invoice, warm | −0.12% to +1.13% | −0.61% to +0.00% |
| Ordinary definitions, through result | −1.50% to +1.24% | −2.29% to −0.61% |

Retain the simpler handoff and modest reduction in per-run allocation. The
defaulted-rule result improves across all pairings, with 4.39% elapsed spread
between the reference workers. The manifest compile-only CPU increase and the
short-command through-result uncertainty remain open. This comparison
establishes no general elapsed or compilation improvement.

Local warm allocation is 32 B lower in six of nine cases. The other three
medians are 152–264 B lower, with variation larger than the removed facade.
Admitted peaks and balance restoration match in all nine. Executable
instructions, spans and retained compiler admission match for 224 code
objects. The original tape-reading tool expected a removed private field.
The corrected tool understands both layouts and compares the exact frozen
images. This affected the inspection tool only.

Both images pass the nine broad and five focused CPython 3.13.2 goldens.
The focused Release regressions cover concurrent execution on one compiled
script, provider disposal, refusal and cancellation. All 224 timed batches
verify and both quiet gates pass. Workers join, temporary fixtures are
removed and the archive verifies under the lease.

[Identities](identity.json) · [Timing](quiet-summary.json) ·
[Allocation](allocation-summary.json) · [Tapes](tape-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Procedures and patch](procedures.tar.xz) ·
[Terminal status](quiet-terminal-status.json).
