# B1599 Select closed integer inputs by source

Private `642c8f957` gives the existing parameter path and the private
capture path distinct input selection while sharing one arithmetic body.
The Windows FullOpts parameter form returns to 1,111 code bytes and a
168-byte stack frame, matching the reference sizes. B1598 used 1,861
code bytes and a 200-byte frame for the shared selector.

| Complete script | Boundary | Elapsed change | Wider process CPU change | Elapsed spread reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Captured rules | Warm | −18.41 to −17.56% | −19.67 to −17.48% | 0.62 / 0.41% |
| Defaulted rules | Warm | −1.85 to +4.12% | −1.56 to +2.29% | 3.48 / 2.52% |
| Eager annotated rules | Warm | −5.73 to +2.74% | −5.89 to +3.07% | 6.29 / 2.53% |
| Postponed annotated rules | Warm | +1.41 to +5.00% | +0.12 to +6.08% | 2.00 / 1.51% |
| Short Unicode report | Warm | −5.30 to +0.23% | −5.06 to −0.18% | 5.20 / 0.62% |
| Quoted invoice | Warm | +1.97 to +2.79% | +0.59 to +1.30% | 0.63 / 0.17% |
| Captured rules | Compile to result | −16.51 to −13.34% | −15.52 to −12.61% | 1.04 / 2.74% |

Both images use the same 32-file layout as B1598 on the quiet Azure
F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 on November 24, 2026 UTC.
The reference DLL is the same as B1598. Ranges cover the four pairings
of two independent worker medians per image. Seven rotated batches follow
eight seconds of warmup per worker. Warm includes applicable mediated
RAM-backed I/O, result verification and previous-result disposal.
Compile to result also includes compilation and compiled-owner disposal.
Wider CPU covers the request. Startup and final teardown are separate.

Captured rules allocate 208 KiB less per run and peak 448 KiB lower in
shared admission. They return to the budget baseline. All five controls
match on local allocation and admission. Both platforms pass 102 focused
Release checks, including shared-budget refusal, cancellation, stack safety
and suspended protocol fallback. Four independent CPython programs and
six script goldens verify on both images. All 196 timed batches, quiet
gates, worker joins, fixture removal and archive verification pass.

Integrate this revision for the repeated captured improvement and real
storage removal. Eager and short-text elapsed ranges cross zero after
isolation. Postponed and quoted-invoice costs remain and carry into the
next delivery comparison. The earlier adverse source and runtime
measurements remain in B1598.

[Timings](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Generated code](jit-summary.json) · [Image identity](identity.json) ·
[Observations](observations.tar.xz) · [Procedures and source](procedures.tar.xz) ·
[Storage custody](storage-custody.json) · [Terminal status](terminal-status.json) ·
[B1598 and reference core](../README.md).
