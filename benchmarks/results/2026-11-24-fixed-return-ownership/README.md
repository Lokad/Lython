# B1566 scalar funding for fixed unpacked returns

An eligible fixed unpack uses the caller's admitted operand slots.
The return now admits retained scalar storage before publishing
the elements. It drops the tuple shell and backing estimate for
the tuple that this path never constructs. Protected and escaping
returns keep ordinary tuple allocation and admission.

| Complete warm script | Lower peak admitted memory | Elapsed change | Process CPU change |
| --- | ---: | ---: | ---: |
| Captured rules | 128 KiB | +0.43 to +1.47% | +0.35 to +2.13% |
| Default rules | 128 KiB | −0.43 to +3.33% | −1.91 to +4.38% |
| Annotated rules, unaffected | 0 | −12.41 to +6.52% | −12.43 to +6.39% |
| Quoted invoice | 384 KiB | −2.17 to +0.12% | −3.20 to +0.72% |

CLR allocation matches on all four scripts. Every shared allowance
returns to baseline. Retain the ownership correction because it
prevents an unnecessary denial and removes admission for nonexistent
storage. Captured rules do take slightly longer in this collection.
The annotated reference workers differ by 17.45%, leaving that
control inconclusive. Carry both observations into the next related
runtime comparison.

Reference `81ce6201e` and candidate `f9d600fe9` differ only in the
core DLL within the verified 32-file images. The quiet Azure F4as_v7
runs Ubuntu 24.04.4 and .NET 11 RC3 on November 24, 2026 UTC. Two
workers per source warm for eight seconds and contribute seven
rotated samples. Warm elapsed includes mediated RAM-file work
where applicable, verification and prior-result disposal. Process
CPU also includes reset and request overhead. Compilation, startup
and final teardown stay outside warm elapsed. All 112 measured
batches match their CPython 3.13.16 goldens. Both quiet gates, worker
joins, fixture cleanup and persistent archive checks pass.

Windows and Ubuntu each pass 180 white-box and 373 public focused
.NET 11 Release checks. The alias case passes under the same pressure
that denies it on the exact frozen reference. The new test also
checks genuine scalar refusal and later reuse. The existing
target-effect regression retains its headroom and source-span
assertions with a numeric return. An initial test exceeded the
bounded direct-unpack width and exercised ordinary tuple allocation.
The final fixture uses eight values within the existing width.

[Timing](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Image identity](identity.json) · [Raw observations](observations.tar.xz) ·
[Procedures, reference regression and exact patch](procedures.tar.xz) ·
[Terminal receipt](terminal-status.json).
