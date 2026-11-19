# B1108 right-trimmed ranges in native split

R676 passes a private right-trimmed UTF-8 range into the existing split
scanner. The prepared recipe applies to the builtin `str.rstrip` with one
ASCII character and an immediate `split` with a literal nonempty separator.
Both execution APIs preserve fixed unpacking, failure spans and cancellation.
Borrowed fields keep the original storage owner and copies use allocation preflights.
Custom receivers and other argument shapes follow ordinary method dispatch.

The pair is `9407f4fb7` versus retained `3e67fd010`. Windows .NET 11 and
Ubuntu .NET 10 each passed 272 focused Release checks. Five CPython goldens
passed on both complete worker images and 14 differential probes agreed
through both execution APIs. The initial span assertion used CRLF fixture
coordinates. The normalized fixture passed on both targets.

| Workload | Boundary | Guest elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Named forwarding | Warm | −7.24…−5.34% | −6.90…−5.04% |
| Inline dynamic forwarding | Warm | −8.03…−4.16% | −8.70…−4.07% |
| Named forwarding | Compile-to-result | −5.11…−3.32% | −5.31…−3.35% |
| Helper text normalization | Warm | −3.61…−0.27% | −2.58…+1.81% |
| CSV invoice | Warm | −1.23…+1.36% | +0.62…+2.74% |
| Ordinary rules | Warm | −0.78…+0.02% | −1.02…+2.55% |

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on
November 19, 2026. Ranges cover all four pairs of duplicate worker medians.
Warm elapsed includes mediated I/O, golden checks and previous-result
cleanup. Compile-to-result adds compilation and compiled-owner disposal.
Process CPU covers the wider request.

Local allocation falls from 2,719,006 to 2,661,558 bytes per named run
(**2.11%**) and from 2,145,614 to 2,088,270 per inline run (**2.67%**).
Each accounted peak falls by 131,104 bytes. Each borrowed allowance returns
to baseline. The forwarding gains support retaining the recipe. Invoice
elapsed remains inconclusive and its higher CPU remains a delivery control.

All 120 timed batches and both quiet gates passed in 324.99 seconds.
Workers joined and the guest fixtures were removed under the VM lease.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) · [Checks and procedures](checks-and-procedures.tar.xz).
