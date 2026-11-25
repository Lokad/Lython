# B1714b Fixed binding reservations on the existing call frame

This candidate keeps B1714's common short and wide operand binding.
The existing call frame and its continuation carry the fixed amount.
This removes the temporary reservation object while keeping admission
before final binding storage and refund after the last user. JSON's
single positional default routes retain their direct entry.

The reference is `60ecf0d3b`. The two 32-file images differ only in the
core DLL and verify six complete CPython 3.13.2 goldens. On November 25,
2026 UTC, the Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3. Two
workers per image warm for five full-request seconds and contribute
seven rotated batches each. Ranges span the four pairings of worker
medians. Warm elapsed includes mediated RAM-backed I/O, output and
file verification, and result cleanup. Wider CPU adds reset and framing.

| Workload and warm phase | Elapsed change | Wider CPU change | Elapsed worker spread reference / candidate |
| --- | ---: | ---: | ---: |
| Default JSON export | −1.19 to +2.78% | −1.08 to 0.00% | 1.00 / 2.98% |
| Three-argument tolerance report | +0.57 to +16.00% | +1.95 to +15.33% | 7.37 / 7.43% |
| Two-option JSON export | −2.25 to −0.37% | −1.71 to −0.87% | 0.31 / 1.61% |
| Two-option JSON export, async | −1.57 to +0.18% | −1.75 to 0.00% | 0.93 / 0.84% |
| Wide-option JSON export | −4.67 to +0.04% | −4.37 to −0.15% | 4.29 / 0.61% |
| Ordinary definitions | −1.06 to +1.28% | −1.83 to +2.86% | 1.03 / 1.33% |

Wide JSON allocates 98,192 B less per execution in the local sync
diagnostic at a matching admitted peak. Short math and JSON no longer
have the allocation increase seen in B1714. Short call admission
can still refuse earlier. The short tolerance report remains slower
across CPU pairings. Its worker spread limits the magnitude estimate.
Keep the shared binding and owner deletion as an interim step.
The next milestone must address the remaining heap binding for
synchronous short builtins and reconsider the math debt before
completing the migration.

Both platforms each pass 1,276 white-box and 2,828 public focused
Release checks. These cover before-body refusal, numeric-hook admission,
live pending arguments, cancellation, error diagnostics and allowance
reuse. Both quiet gates and all 168 timed batches pass. Workers join,
guest fixtures are removed and the archive verifies under its lease.

[B1714](../README.md) · [Identity](identity.json) · [Timing](quiet-summary.json) ·
[Allocation](allocation-summary.json) · [Observations](quiet-observations.tar.xz) ·
[Terminal receipt](quiet-terminal-status.json) · [Procedures](procedures.tar.xz).
