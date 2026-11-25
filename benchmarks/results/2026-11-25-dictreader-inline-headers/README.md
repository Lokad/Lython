# B1718c Isolate DictReader fieldname completion

The candidate completes known fieldnames and completed header reads inline.
Deferred header reads still await the source. Data-row transport retains
the reference sequence. The reference is `36a4c681f`, which includes the
blank-row refund correction. Both frozen images verify all four complete
script goldens and differ only in the core DLL.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 25,
2026 UTC. Each of the two workers per image warms for five full-request
seconds and contributes seven batches per phase. The table spans the four
pairings of worker medians. Warm elapsed includes mediated RAM-backed I/O,
verification and cleanup. Wider CPU also includes reset and request framing.

| Workload and phase | Elapsed change | Wider CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Nonlocal text control, warm | −8.94 to −3.86% | −8.38 to −4.84% | 1.11 / 4.42% |
| File-fed map, warm async | −4.99 to +5.13% | −6.72 to +5.12% | 0.82 / 9.75% |
| Quoted invoice, warm | −4.40 to −0.44% | −3.90 to −1.30% | 3.05 / 1.06% |
| Plain reader control, warm | −5.93 to −1.43% | −5.67 to −2.04% | 1.80 / 2.93% |
| File-fed map, warm sync | −0.28 to +0.70% | −2.95 to +1.35% | 0.08 / 0.89% |

The unchanged controls also use less CPU, with larger observed changes than
quoted invoice. Both file-fed CPU ranges cross zero. Compilation allocation and
retained admission match, as do execution peaks and managed allocations on
the affected scripts. Keep the source private. The isolated timing leaves
the fieldname wrapper's cost unresolved and the supported source keeps one
completion method.

Windows .NET 11 and Ubuntu .NET 10 each pass 43 focused tests, covering
consecutive deferred blanks, a fieldname change during a suspended pull,
header alias funding, cancellation and allowance recovery. All 140 timed
batches and both quiet checks pass. The persistent archive verifies after
workers join and fixtures are removed.

[Identity](identity.json) · [Timing](quiet-summary.json) ·
[Runtime allocation](allocation-summary.json) · [Compiler allocation](compile-allocation-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Terminal receipt](quiet-terminal-status.json) ·
[Procedures and patch](procedures.tar.xz).
