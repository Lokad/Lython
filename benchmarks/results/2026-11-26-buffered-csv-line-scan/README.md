# B1762 Describe literal CSV rows directly from decoded windows

The patch uses the existing literal field scanner to find the physical
terminator for complete rows in universal-newline text windows without
numeric coercion. The file advances before publication and admission.
Complex fields and partial windows discard the speculative descriptors and
use the existing parser. That fallback can repeat part of the scan.
[B1761 route counts](../2026-11-26-csv-window-source-gate/README.md).

| Workload | Warm API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Shipment callback report | Async | −9.25 to +20.40% | −13.70 to +0.10% |
| File-fed integer quantity map | Async | −16.27 to −2.61% | −16.43 to +33.62% |
| Unchanged Unicode text report | Sync | −9.62 to +60.48% | −3.23 to +97.22% |
| Quote-heavy CSV policy invoice | Async | −30.85 to −14.84% | −25.00 to +20.00% |
| Shipment callback report | Sync | −23.30 to +70.62% | −26.65 to +26.67% |

The reference is `bc93d4d74`. The November 26, 2026 UTC block ran on local
WSL Ubuntu 24.04 pinned to one logical processor with .NET 11 RC3. Two
workers per source contribute seven rotated batches each after four
seconds or more of full-request warmup. Changes span the four pairings
of worker medians. Warm elapsed includes mediated RAM-backed I/O, output
and file checks, and result disposal. Wider process CPU includes reset and
framing. All 140 batches completed, workers joined, fixture cleanup finished
and the persistent archive verified.

All process CPU ranges span zero. Same-source CPU spread reaches 93.75%
on the unchanged text control. The file-fed and invoice elapsed ranges
favor the trial, with unstable controls. Leave the prototype unmerged.
A quiet block could distinguish the route change from local variation.

Both full 32-file images pass independent CPython output and file goldens
on five workloads. Local allocation differs by at most 174 B per run.
Peak admission matches exactly and disposal returns the allowance to
baseline. Windows .NET 11 passes 425 public and 179 white-box focused
Release checks, including mixed newline styles, a CRLF split across the
read boundary, blank and multiline fields, line numbers and subsequent
file reads. Both newline cases also agree with CPython. The first isolated
test snapshot omitted linked benchmark sources and representative script
fixtures. Both initial failures and the corrected runs are preserved.

[Timing](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Identity](identity.json) · [Observations](observations.tar.xz) ·
[Receipt](archive-receipt.json) · [Patch, regression and procedures](procedures.tar.xz).
