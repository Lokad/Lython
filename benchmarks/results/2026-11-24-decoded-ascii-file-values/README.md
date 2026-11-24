# B1580 Reuse immutable ASCII values in decoded text reads

`f8aa4fba8` retains the measured source from `36520e2c0`, compared with
`f01360b7f`. Complete one-byte reads reuse the immutable ASCII values
already used by string indexing and iteration. When EOF completes an
accumulated one-byte read, its builder releases its capacity before
returning the same immutable value.

| Complete script | CLR bytes per warm run, reference → candidate | Admitted peak, reference → candidate | Warm elapsed change | Wider CPU change | Elapsed spread, reference / candidate |
| --- | ---: | ---: | ---: | ---: | ---: |
| Short nonlocal report | 380,488 → 323,144 | 735,891 → 603,795 | −2.74 to +0.73% | −3.14 to −0.28% | 2.71 / 0.83% |
| Nonblank report | 438,968 → 438,968 | 869,586 → 869,586 | −0.54 to +6.33% | −0.60 to +7.25% | 4.66 / 2.15% |
| Mixed-width histogram | 454,288 → 396,944 | 806,570 → 674,474 | −3.96 to +4.12% | −3.34 to +5.46% | 6.34 / 1.94% |
| Distinct-word histogram | 1,650,222 → 1,650,222 | 2,105,910 → 2,105,910 | −2.44 to +5.57% | −3.12 to +4.82% | 5.41 / 2.66% |
| Quoted invoice | 3,280,920 → 3,280,914 | 9,040,266 → 9,040,266 | −1.31 to +5.28% | 0.00 to +5.56% | 1.06 / 5.56% |

The short report and mixed-width histogram each encounter 1,024 one-byte
lines. They avoid 57,344 CLR bytes per run and lower peak admission by
132,096 bytes. Six local warm invocations per case and image restore the
budget baseline. The short report's wider CPU falls across all pairings.
Every elapsed range spans zero. The adverse control pairings and up to
6.34% worker spread limit the timing conclusions. Retain the identity and
allocation correction and keep these timing ranges for the next comparable
whole-script checkpoint.

The direct CPython 3.13.2 file probe returns shared identity for LF and
translated CRLF. Both public regressions fail at `f01360b7f` and pass with
the patch, exercising iteration, bounded reads and a final unterminated
byte. The tests check results after sync and genuinely asynchronous close
and require shared allowance to return to baseline. Windows and Ubuntu
each pass 100 white-box and 139 public .NET 11 Release checks.

Both 32-file images match five CPython 3.13.16 goldens and differ only in
the core DLL. The Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on
November 24, 2026 UTC. Two workers per source warm for eight seconds and
provide seven rotated samples each. The ranges span pairings of their
medians. Warm elapsed includes applicable mediated RAM-backed file I/O,
output verification and previous-result disposal. Wider process CPU also
includes request framing and fixture reset. The comparison uses already
compiled scripts in running processes.

All 140 batches complete in 259 seconds. Both quiet gates, worker joins,
guest fixture removal and persistent archive verification pass. Staging
reuses unchanged files from an immutable image and retires five VM
replicas after verifying their persisted cores and observation archives.

[Timing](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Image identity](identity.json) · [Raw observations](observations.tar.xz) ·
[Procedures and exact patch](procedures.tar.xz) · [Terminal status](terminal-status.json).
