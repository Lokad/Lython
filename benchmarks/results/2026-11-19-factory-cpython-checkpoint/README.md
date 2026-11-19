# B1151 function factories against CPython

Compared the executable sources of Lython `0d74b5b18` against stock
CPython 3.13.16 on November 19, 2026 UTC, on the quiet Azure F4as_v7 with
Ubuntu 24.04.4 and .NET 11 RC3. The Lython image was frozen at `249d5522b`
for B1149. All 32 files were verified, and the executable sources match
the delivery revision. Both engines matched all three output goldens.
The factory workloads use 2,048 inputs. The nested variant creates a fresh
factory and then an annotated function on every iteration. The invoice
uses 4,096 data records and 127,031 input bytes.

| Workload | Boundary | Lython | CPython | Lython / CPython |
| --- | --- | ---: | ---: | ---: |
| Annotated rules | Warm | 1.519–1.552 ms | 0.411–0.429 ms | 3.54–3.77× |
| Annotated rules | Compilation | 0.02195–0.02196 ms | 0.03141–0.03231 ms | 0.68–0.70× |
| Annotated rules | Compile-to-result | 1.563–1.582 ms | 0.440–0.453 ms | 3.45–3.59× |
| Nested factories | Warm | 2.041–2.106 ms | 0.583–0.599 ms | 3.40–3.61× |
| Nested factories | Compilation | 0.02682–0.02716 ms | 0.03863–0.03890 ms | 0.69–0.70× |
| Nested factories | Compile-to-result | 2.107–2.182 ms | 0.629–0.643 ms | 3.28–3.47× |
| CSV invoice | Warm | 6.474–6.546 ms | 6.126–6.184 ms | 1.05–1.07× |

Ratios divide Lython elapsed by CPython elapsed across the four pairings of
duplicate process medians. Warm CPython controls for ordinary annotated
rules differ by 4.38%. The nested Lython controls differ by 3.18% warm and
3.58% compile-to-result. These diagnostic ranges show the execution gap is
still substantial. The comparison between Lython revisions is recorded
separately. [B1149 and B1150](../2026-11-19-compiled-definition-keys/README.md).

Warm includes output verification, prior-result cleanup and any mediated
RAM-backed file I/O. Compilation includes disposal of the compiled owner.
Compile-to-result measures compilation, one execution and cleanup. Startup,
fixture reset, framing and final teardown are outside guest elapsed. Process
CPU covers the wider request. Lython charges storage to a shared allowance
and uses the host's filesystem. CPython uses native file I/O.

All 140 timed batches passed in 172.34 seconds. Both quiet gates passed,
workers joined, fixture mounts were removed and the persistent archive
was verified under the lease. The initial collection stopped at startup
because the staging identity omitted its auxiliary-file map. Its failure
was archived before unmounting its fixture directory. The corrected
collection used the same complete image, scripts and inputs.

[Measurements](summary.json) · [Raw observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) · [Procedures](procedures.tar.xz) ·
[Initial failure and recovery](initial-failure.tar.xz).
