# B1753 Ready async shipment rows

The trial returns completed `starmap` rows and zip fixed-unpack results
directly. A genuinely incomplete pull or callback still uses the existing
row and argument owners. The two production files and the focused test are
preserved as patches. The reference is `7ce34e1fa`, whose runtime source
matches `3507bdd2d`. [Preceding CPU attribution](../2026-11-26-remaining-shipment-cpu/README.md).

| Workload, 2,048 records each | Lane | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Bound shipment with `starmap` | Warm async | −3.44 to +0.13% | −3.14 to +0.45% |
| Shipment retaining zip tuples | Warm async | −4.53 to +1.00% | −3.87 to +0.50% |
| Ordinary shipment with `map` | Warm async | −2.10 to 0.00% | −2.54 to +0.54% |
| Bound shipment with `starmap` | Warm sync | −2.03 to +1.73% | −1.61 to +1.44% |

The November 26, 2026 UTC block uses the quiet Azure F4as_v7 with Ubuntu
24.04.4 and .NET 11 RC3. Two workers per source supply seven rotated
measurement batches each after at least seven seconds of warmup. The ranges
cover the four pairings of process medians. Negative values favor the
candidate. Warm execution includes mediated RAM-backed file I/O, verified
output and files, and result disposal. Process CPU includes fixture reset
and framing. All 112 batches completed, both quiet checks passed, all
workers joined and the archive verified under the lease.

Both immutable images passed independent CPython output and file checks on
four cases. Matching local diagnostics returned the host allowance to
baseline. Peak admission matched on all four, and the only observed CLR
allocation difference was six bytes on ordinary shipment. Windows .NET 11
and Ubuntu .NET 10 each passed 560 public and 285 white-box focused checks
on the trial.

CPU ranges span zero on targets and controls. The ordinary shipment elapsed
range ends within 0.001 percentage points of zero. Keep the evidence and
leave the production patch unmerged. It adds separate continuation entry
points without demonstrating a whole-script improvement.

[Timing](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Identity](identity.json) · [Observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Patches and procedures](procedures.tar.xz).
