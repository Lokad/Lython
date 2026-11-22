# B1317 Reuse decoded JSON string suffixes

The scanner validates UTF-8 in bounded raw runs as it locates quotes,
controls and escapes. It keeps the decoded suffix after the first
escape in bounded managed scratch when it fits. It admits one exact
final string after complete syntax validation. Wide suffixes replay
into final storage. Plain strings still reuse funded input ranges or
copy borrowed data. The candidate retains the original error
precedence and admission timing.

Compared `8c81fb8d2` with `e5316a8de` on November 22, 2026 UTC on
the quiet Azure F4as_v7 running Ubuntu 24.04.4 and .NET 11 RC3.
Two independently warmed processes per revision supplied seven
rotated batches each. Ranges cover all process-median pairings.
Elapsed includes mediated RAM-backed I/O and prior-result disposal.
Process CPU includes worker request handling. Negative changes
favor the candidate.

| Whole warm script | Elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Escaped-Unicode formatted report | −4.02 to +1.86% | −3.28 to +0.59% | 1.39 / 4.67% |
| Plain formatted report | −6.82 to −0.35% | −6.49 to −1.13% | 6.50 / 0.41% |
| JSON normalization | −1.25 to +0.32% | −1.45 to −0.44% | 0.80 / 0.79% |
| Unchanged mixed-Unicode text report | −0.91 to +3.61% | −0.41 to +2.51% | 2.22 / 2.29% |

Both clocks for the escaped report span zero. The candidate processes
differ by 4.67% on elapsed. The plain report uses less CPU, but its
reference medians differ by 6.50%. Normalization has a small CPU
reduction and mixed elapsed. The unchanged text control spans zero.
The candidate stays private while a bounded confirmation checks the
two reports with longer warm-up. All original observations remain.

Local allocation differs by less than 0.01% across the four cases.
Accounted peaks match and each allowance refunds to its baseline.
Windows .NET 11 and Ubuntu .NET 10 each pass 217 white-box and 906
public focused Release checks. A 220-case CPython compatibility
matrix matches on both run paths, covering Unicode around scan
and suffix boundaries, malformed escapes, permissive and strict
controls and original scalar error coordinates.

Both 32-file images match four CPython goldens before timing. All
112 batches verify. Workers join, RAM fixtures retire and the
persistent archive verifies under the lease in 223 seconds.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Source and checks](checks-and-procedures.tar.xz) ·
[Reference core](../2026-11-22-sparse-lexical-preparation/candidate-core.dll.gz).
