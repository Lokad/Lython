# B1393 Replay literals from the original regex template

The quiet Azure F4as_v7 compared frozen `c097d73db` and private
`8bdf5f43f` on November 22, 2026 UTC with Ubuntu 24.04.4 and .NET 11 RC3.
Only the core DLL differs between the two verified 32-file images.
Both use the optional PythonRe 0.4.0 adapter.

The candidate borrows ordinary literal ranges from the immutable
UTF-8 replacement template and replays recognized escapes individually.
It removes the decoded literal builder and its full-array copy.
Named lookup reserves only the temporary bytes needed by the current
name. Final output obtains its own admission.

| Complete script, warm | Elapsed change | Wider CPU change | Worker spread reference / candidate |
| --- | ---: | ---: | ---: |
| Rewrite 96 file lines using named groups | +0.29 to +1.15% | −1.03 to +0.53% | 0.43 / 0.42% |
| Expand a 12 KiB literal template 48 times | −21.75 to −19.76% | −21.77 to −20.68% | 0.29 / 2.23% |
| Expand an escape-dense template 48 times | +13.43 to +26.83% | +15.40 to +29.34% | 10.74 / 0.97% |
| Expand plain literal 48 times | −0.31 to −0.03% | −0.64 to +0.02% | 0.07 / 0.21% |
| Positional call control | −1.60 to +0.73% | −2.50 to +1.52% | 0.94 / 1.41% |

Ranges span all pairings of two process medians per source after
eight seconds of warmup and seven rotated batches per process.
Warm clocks include applicable mediated RAM-backed I/O, output
verification and prior-result disposal. Wider CPU includes reset
and protocol. Compilation and process startup are outside the clocks.

The long template allocates 45.09% less in the local diagnostic.
Its peak admitted bytes fall from 878,561 to 829,349. The file rewrite
allocates 0.54% less with the same admitted peak. Dense escapes allocate
1.94% more while their admitted peak falls from 49,814 to 47,510.
Both unchanged allocation controls match. The initial local elapsed
readings vary substantially, including the positional control.

Keep this version private. The quiet results establish a long-literal
benefit and an escape-dense regression despite reference variability.
The file rewrite takes slightly longer with stable workers and its
CPU spans zero. The next version will compile adjacent escapes into
funded UTF-8 runs and reuse one temporary name reservation. It will
keep ordinary literal ranges borrowed. Confirm with the same scenarios.

Both platforms passed 180 white-box and 616 public Release checks,
including expansion beside a funded 32 KiB template. The first public
runs found another revision's subprocess under the artifact directory
and failed its identity checks. Matched-artifact reruns verify all
three cancellation stages. Both exact timing images agree with the
CPython goldens and refund their allowance. All 140 timed batches
verify. Both quiet gates pass, workers join and the persistent archive
verifies under the lease.

[Source and images](source.json) · [Timings](summary.json) ·
[Local allocation](allocation-summary.json) · [Raw observations](observations.tgz) ·
[Tests and procedures](procedures.tar.xz) · [Archive receipt](archive-receipt.json).
