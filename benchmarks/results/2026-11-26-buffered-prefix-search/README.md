# B1813 Check rejected file lines within the decoded window

The candidate on `d5d8a973d` recognizes the
existing counted search loop with an exact one-argument constant-prefix
helper. It examines already-decoded complete lines. A group of rejected
lines publishes one ordinary Python string and the updated count. Matching
lines, window boundaries and dynamic callables use the original path. The
grouping requires a reservation whose resize runs no host code. This is an
eligibility proof for the optimization. External reservations continue
through ordinary per-line publication.

Windows .NET 11 and Ubuntu .NET 10 each pass 69 white-box and
177 public focused Release checks. The new cases cover first and late
matches, misses with both kinds of EOF, mixed newlines, a line crossing
reader windows and a rebound helper. Each case checks the final bindings,
output and joined close. CPython 3.13.2 independently matches these five
cases. Both 32-file measurement images also match three CPython goldens.

The first test run used an async-only host for synchronous cases. It
failed before reaching any search. The rerun uses the synchronous host
for `Run` and the delayed host for `RunAsync`. Both original logs survive.
The first standalone tape-inspection command also needed an explicit
framework because the repository sets two targets.

## Allocation and admission

Windows .NET 11 runs both APIs with shared-budget accounting and verifies
the allowance returns to baseline. The two search inputs contain identical
bytes in a different line order and write identical output.

| Workload | Mode | CLR bytes, reference → candidate | Peak admitted bytes, reference → candidate |
| --- | --- | ---: | ---: |
| Match at line 1,025 | sync | 113,280 → 56,160 | 167,072 → 36,512 |
| Match at line 1 | sync | 39,288 → 39,288 | 19,556 → 19,556 |
| Unicode file-report control | sync | 322,992 → 322,992 | 603,795 → 603,795 |
| Match at line 1,025 | async | 113,496 → 56,408 | 167,072 → 36,512 |
| Match at line 1 | async | 39,536 → 39,536 | 19,556 → 19,556 |
| Unicode file-report control | async | 323,240 → 323,240 | 603,795 → 603,795 |

## Local timing

Ubuntu WSL runs two workers per image, with three seconds of warmup and
seven rotated timed batches per worker. Ranges span all four pairings of
worker medians. Negative values mean less time. Warm elapsed includes
mediated RAM-backed I/O, verification and disposal of the previous result.
Through-result timing also includes compilation and compiled-script disposal.
Process CPU covers fixture reset and request framing as well. Process
startup stays outside these clocks. All 140 batches completed, workers
joined, fixtures were removed and the persistent archive verified.

| Workload | Phase | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Match at line 1,025 | Warm async | −50.22% to −43.66% | −41.49% to −37.42% |
| Unicode file-report control | Warm async | −13.54% to +10.69% | −17.21% to +13.78% |
| Match at line 1,025 | Warm sync | −56.31% to −44.33% | −53.36% to −39.65% |
| Match at line 1 | Warm async | −2.20% to +7.47% | −0.28% to +7.12% |
| Match at line 1,025 | Through result | −38.58% to −31.76% | −38.55% to −33.94% |

## Local control repeat

The same source images run 56 more timed batches with six seconds of
warmup per worker. The repeat covers the early match and the file report
to characterize the added dispatch on unchanged allocation.

| Workload | Phase | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Unicode file-report control | Warm async | −11.52% to +36.09% | −12.97% to +25.79% |
| Match at line 1 | Warm async | +3.90% to +12.76% | +1.75% to +11.74% |

Keep this version private pending refinement. For the late match it
cuts allocator traffic by about half and lowers peak admission by
78.15%. Warm elapsed falls 43.66–50.22% async and 44.33–56.31% sync.
Through-result elapsed falls 31.76–38.58%. The early and Unicode
controls allocate the same bytes. The early-match repeat uses
3.90–12.76% more elapsed time and 1.75–11.74% more process CPU.
The Unicode control has a wide spread in both runs. Move the static
loop-shape proof out of the runtime path and check the early case again
before deciding whether to retain the optimization.

[Allocation and admission](allocation-summary.json) · [Timing and spreads](summary.json) ·
[Frozen image hashes](identity.json) · [Raw observations](observations.tar.xz) ·
[Archive receipt](archive-receipt.json) · [Repeat timing](confirmation/summary.json) ·
[Repeat observations](confirmation/observations.tar.xz) · [Focused checks, catalogs, tape and exact patches](procedures.tar.xz).
