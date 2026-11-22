# B1424 Classify literal CSV fields once

The native literal-record parser now identifies quoted fields at the
field start. Bare fields search only for a delimiter or line ending.
Interior quotes remain ordinary bytes. Incomplete quotes, transformed
fields and scratch growth resume the existing parser at the current
field. Field materialization, numeric conversion, row ownership and
publication order stay on the same path.

Compared `9946d48aa` and `9e3b826df` on the quiet Azure F4as_v7,
Ubuntu 24.04.4 and .NET 11 RC3 on November 22, 2026 UTC. The two
32-file images differ only in the core library. `d14f36ef7` retains
the measured code and the native-input regression.

| Complete script | Boundary | Elapsed change | Wider process CPU change |
| --- | --- | ---: | ---: |
| Quote-heavy CSV invoice | Warm | −1.88 to +1.25% | −2.25 to +0.40% |
| Quote-heavy CSV invoice | Compile to result | −1.83 to −0.31% | −2.27 to +0.71% |
| Ordinary CSV invoice | Warm | −1.47 to −0.43% | −1.14 to +0.40% |
| Typed ASCII meter | Warm | +0.32 to +2.42% | +0.49 to +2.94% |
| Typed Unicode meter | Warm | −5.75 to −1.27% | −6.41 to −2.09% |
| CSV restock export | Warm | −0.88 to +0.88% | −1.84 to +1.43% |
| Nonlocal word-count report | Warm | −0.62 to +2.70% | −0.67 to +4.06% |

The ranges span pairings of medians from two independent processes
per source. Workers warm for eight seconds and complete seven rotated
batches per lane. Warm elapsed includes mediated RAM-backed fixture
I/O, output verification and previous-result disposal. Compile to
result adds compilation and compiled-owner cleanup. Wider CPU includes
fixture reset and protocol. Process startup and final teardown are
outside the elapsed clocks. This capture adds no CPython timing ratio.

Retain the simpler scan and the lower Unicode meter CPU. The ASCII
elapsed and CPU regression remains open. Quote-heavy warm elapsed
spans zero and its candidate workers differ by 3.02%. The untouched
text control also spans zero for both clocks and allows an adverse
CPU result. Local allocations are effectively flat in all six cases.
Admitted peaks match and allowances return to baseline.

Windows .NET 11 and Ubuntu .NET 10 each pass 151 white-box and 449
public focused Release checks. A separate 1,484-case matrix exercises
native parser rows, quoted newlines, Unicode syntax, leading empty
fields, errors and retained and peak accounting. The initial candidate
omitted a leading empty field in nine cases. The corrected candidate
matches all 1,484 cases and the maintained native-input regression
now covers that row shape. Both frozen images verify the six complete
CPython goldens.

All 196 timed batches and both quiet gates pass in 388 seconds.
Workers join, fixtures are removed and the persistent archive verifies
under the lease. Staging removes only the older working DLLs whose
exact bytes and observations are already preserved.

[Sources and images](source.json) · [Timings](summary.json) ·
[Local allocation and admission](local-allocation.json) · [Raw observations](observations.tgz) ·
[Checks and procedures](procedures.tar.xz) · [Archive receipt](archive-receipt.json).
