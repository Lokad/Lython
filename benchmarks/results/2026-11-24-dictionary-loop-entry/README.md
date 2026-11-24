# B1573 Counting-loop entry before unused body setup

Private `31f3ccadd` checks for exhaustion before loading the loop
body's metadata. It uses the existing exit and retirement for both
counting paths. The short nonlocal report enters 1,024 empty word
loops per invocation. The earlier empty-tuple representation change
is outside this comparison.

| Complete script | Warm elapsed change | Wider CPU change | Elapsed spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Short nonlocal report with blank lines | −9.52 to +0.63% | −8.64 to +0.40% | 7.04 / 3.89% |
| Short nonlocal report without blank lines | −6.11 to +1.80% | −4.02 to +1.78% | 6.88 / 1.45% |
| Mixed-width word report | −1.47 to +4.24% | +0.64 to +3.77% | 3.43 / 2.29% |
| Quoted invoice | −0.46 to +1.42% | −2.56 to −0.69% | 0.34 / 1.54% |

Retain the evidence and keep the patch private. Both short-report
elapsed and CPU ranges span zero. Similar reference movement on the
nonblank report prevents attributing the lower medians to the early
exit. Mixed-width CPU is adverse. Continue with the shared arithmetic
path as a separate possible source of the B1572 control debt.

Windows and Ubuntu each pass 1,302 focused .NET 11 Release checks.
The suites cover cancellation, refused admission, word binding and
dictionary mutations. Both images pass four CPython 3.13.16 goldens.
Admitted peaks match and allowances return to baseline. The quoted
invoice's local allocation diagnostic differs by 56 B per invocation
and the three text allocations match.

Frozen `768cd861d` and `31f3ccadd` differ only in the core DLL
across 32 verified image files. The reference runtime matches
`df89d5b6b`. Two workers per source on the dedicated Azure F4as_v7
with Ubuntu 24.04.4 and .NET 11 RC3 warmed for eight seconds and
alternated seven samples each on November 24, 2026 UTC. Elapsed
includes applicable mediated RAM-file I/O, golden verification and
prior-result disposal. Wider CPU additionally includes request and
reset overhead.

A local setup attempt stopped while writing the collector because the
working directory was absent. The corrected setup reused the verified
images. All 112 timed batches pass in the resulting 211-second
collection with image and quiet checks, worker joins, fixture removal
and archive verification. Two verified archived core replicas made
room for staging.

[Timing](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Image identity](identity.json) · [Raw observations](observations.tar.xz) ·
[Procedures and exact patch](procedures.tar.xz) · [Terminal status](terminal-status.json).
