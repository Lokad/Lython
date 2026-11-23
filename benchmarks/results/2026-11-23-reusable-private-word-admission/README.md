# B1449 Reuse private word allowance

`dfabddb88` retains the ownership change measured at `79c38cfab`.
The private casing cursor admits a batch before iteration. Constructed
words take their charge into execution ownership. The cached cursor
reuses admission for words it never materialized. Retiring the cursor
returns its encoded buffer and reusable allowance as one charge.

Compared `726ff7c8c` and `79c38cfab` on November 23, 2026 UTC on
the quiet Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3. Two workers
per revision warmed for eight seconds and supplied seven rotated
batches each. The ranges cover all pairings of worker medians. Warm
elapsed includes mediated RAM-backed I/O, verification and previous
result cleanup. Wider process CPU includes fixture resets and
request handling. Compilation, startup and final teardown sit outside
those clocks. CPython provides the semantic goldens.

| Complete script | Warm elapsed change | Wider process CPU change | Elapsed spread reference / candidate |
| --- | ---: | ---: | ---: |
| Nonlocal Unicode word report | +0.82 to +3.41% | +0.92 to +2.91% | 2.11 / 0.44% |
| Dense Unicode text | −3.79 to +7.79% | −2.84 to +5.65% | 6.78 / 4.92% |
| Wide-vocabulary word report | −4.96 to +2.04% | −5.05 to +3.53% | 3.57 / 3.67% |
| Materialized wide split | −0.19 to +0.43% | −1.68 to −0.17% | 0.39 / 0.23% |
| Plain split | −3.15 to +1.06% | −3.21 to +2.14% | 2.05 / 2.25% |
| Quote-heavy invoice | +0.65 to +2.46% | −0.43 to +4.20% | 1.05 / 0.75% |

Retain the memory improvement with the measured nonlocal CPU cost.
Local admitted execution peaks fall from 1,278,972 to 735,891 bytes
on the nonlocal report and from 2,198,764 to 1,445,257 bytes on the
wide-vocabulary report. CLR allocation is effectively flat. The
separate execution-budget probe observes 5,179 positive admissions
for each nonlocal-report worker and 8,778 for each wide-vocabulary
worker. Admission requests are smaller. The nonlocal report's
accumulated positive growth falls from 1,301,047 to 757,966 bytes.
At a 1,000,000-byte shared allowance including compilation and
projection, the candidate returns the expected output and file bytes
and the reference refuses admission. Both return their allowance
after disposal. The quote-heavy invoice shares the benchmark
environment; its measured elapsed increase remains visible.

Windows .NET 11 and Ubuntu .NET 10 each pass 53 white-box and 29 public
focused Release checks. The new coverage checks reusable private
capacity and retained word funding. Both 32-file images match seven
CPython goldens. All 168 timed batches and the two quiet gates pass.
Workers join, the archive verifies under the quiet lease and guest
fixtures are removed. The procedures preserve the initial compiler
failure from removing an ownership-test accessor and its correction.

[Timings](summary.json) · [Allocation and admitted peaks](local-allocation.json) ·
[Execution admission requests](host-execution-admissions.json) ·
[Shared allowance check](shared-allowance-check.json) · [Exact images](source.json) ·
[Raw observations](observations.tgz) · [Checks and procedures](procedures.tar.xz).
