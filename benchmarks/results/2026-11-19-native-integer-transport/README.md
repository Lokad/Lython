# B1135 native integer results and failures

R706 retains the direct expected-failure route from [B1134](../2026-11-19-native-conversion-failure/README.md).
It also keeps a bounded signed decimal parse in the native integer slot,
removing the round trip through `BigInteger` before a guest needs the
object. The general UTF-8 parser still owns magnitude admission, exact
promotion and syntax. Both images include the admitted numeric error
renderer, so the comparison holds that correction constant.

On November 19, 2026 UTC, the quiet Azure F4as_v7 with Ubuntu 24.04.4 and
.NET 11 RC3 compared `365335ea8` with the retained `e20604ef4`. Both
complete 32-file images verified all five CPython 3.13.16 goldens. Four
independent workers supplied five batches per case. Warm elapsed includes
mediated I/O, golden checks and prior-result cleanup. Process CPU covers
the wider batch. Ranges cross both candidate medians against both
reference medians. Negative values mean less time.

| Complete workload | Warm elapsed change | Process CPU change |
| --- | ---: | ---: |
| Generated validator, six malformed fields | −20.45% to −18.81% | −14.26% to −14.22% |
| Same validator, all fields valid | −2.01% to −0.70% | −3.75% to −1.31% |
| Config manifest | −1.57% to +0.92% | −1.24% to +2.50% |
| CSV invoice | −1.00% to +0.40% | −1.96% to +1.35% |
| In-memory rule closures | −0.60% to +2.79% | −2.59% to +1.19% |

Local allocation on the malformed validator falls from 79,816 to 75,976
bytes per run (4.81%). All other allocation changes round to zero and all
accounted peaks match. The CSV and valid-input CPU slowdowns from B1134
did not recur in this pair. The manifest, invoice and rule timing ranges
span zero, and the two candidate rule workers differ by 2.30% in elapsed
time. The original adverse controls remain part of the evidence.

Windows .NET 11 and Ubuntu .NET 10 each passed 725 focused Release
checks, including conversion errors, ordered `finally`, exception
chaining and identity, refusal and allowance reuse. Both quiet gates
passed. The 272-second collection joined all workers, removed guest
fixtures and verified the persistent observations under lease.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) · [Checks and procedures](checks-and-procedures.tar.xz).
