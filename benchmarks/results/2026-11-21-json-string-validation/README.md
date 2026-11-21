# B1287 single-pass JSON string validation

The prototype scans for raw control bytes, quotes, escapes and non-ASCII
bytes in bounded chunks. It validates Unicode scalars during the scan
instead of validating the complete string a second time. Escaped strings
still use a sizing pass followed by an exact admitted output allocation.
The fill parses the escapes again. Plain string sharing and key reuse
are unchanged.

The exact reference `256c5e651` and prototype `7f61bc52a` ran on
November 21, 2026 UTC on the quiet Azure F4as_v7 VM with Ubuntu 24.04.4
and .NET 11 RC3. Each lane used two independently warmed processes per
source and seven rotated batches from each process. The ranges cover all
four process-median pairings. Warm elapsed includes mediated RAM-backed
file I/O, guest execution and disposal of the prior result. Process CPU
also covers request and fixture handling. Compilation, process startup
and final teardown are outside the warm clocks.

| Complete script | Warm elapsed change | Warm process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| JSON Lines export with expanded options | −2.30% to +0.56% | −3.88% to −0.27% | 0.78% / 2.13% |
| JSON normalization | −2.51% to −1.03% | −3.06% to −0.89% | 0.65% / 0.87% |
| Plain formatted report | −7.04% to −2.68% | −5.92% to −2.67% | 3.25% / 1.40% |
| Escaped-Unicode formatted report | +2.23% to +4.18% | +0.78% to +4.25% | 0.87% / 1.03% |
| Mixed-Unicode text control | −2.01% to +2.79% | −3.54% to +2.50% | 3.57% / 1.28% |

The plain and escaped reports use the same program and expected output.
The escaped input includes 3,072 Unicode escapes in 1,024 string tokens,
with no non-ASCII input bytes. The plain input contains no backslash escapes
and has 6,144 non-ASCII bytes. These fixture counts narrow the adverse case
to escape-heavy input. Whole-script samples do not isolate its cause.

Keep this prototype private. Normalization and the plain report improve
on both elapsed and CPU. JSON Lines CPU falls while elapsed spans zero.
The escaped report slows down across all pairings, with tight duplicate
controls. The text control spans zero. A refinement should preserve the
single validation scan and remove work in the escaped-token path.
Windows micro-observations are separate. Identical-image comparisons
move materially there, so those measurements do not establish the gain.

Windows .NET 11 and Ubuntu .NET 10 each pass 217 white-box and 903 public
Release checks for the exact candidate. Both complete 32-file images
match five CPython goldens. The VM run verifies all 140 timed batches,
passes both quiet gates, joins all workers, removes RAM-backed fixtures,
and verifies the persistent archive under the lease.

[Timing summary](summary.json) · [Local micro-observations](micro-summary.json) ·
[Raw VM observations](observations.tgz) ·
[Source, focused checks and procedures](checks-and-procedures.tar.xz).
