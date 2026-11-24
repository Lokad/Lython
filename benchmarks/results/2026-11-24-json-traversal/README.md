# B1586 Drain eager JSON in the shared traversal

Production `2cf721f73` retains the runtime source measured at `ccdf43a59`.
The shared JSON traversal now consumes completed tokens and frame pushes
within one eager traversal call. `dumps` no longer awaits a two-Boolean
result for each scalar, delimiter or frame push. `iterencode` and native
`dump` still yield or write each complete chunk before advancing. The same
frames, item cursors, cycle checks and builder govern both modes.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 with .NET 11 RC3 on
November 24, 2026 UTC. The 32-file images for `ffa2f83f4` and `ccdf43a59`
differ only in the core DLL. Both verify the same five CPython 3.13.16
goldens. Each image has two independently warmed workers with eight-second
warmups and seven rotated batches. Ranges span pairings of worker medians.
Warm elapsed includes mediated RAM-backed I/O, verification and prior-result
cleanup. Wider CPU includes reset and protocol. Compilation, startup and
final teardown sit outside both clocks.

| Warm script | Elapsed change | Wider process CPU change | Elapsed worker spread, reference / candidate |
| --- | ---: | ---: | ---: |
| JSON Lines, expanded options | −2.94 to −0.86% | −1.16 to 0.00% | 0.54 / 1.60% |
| JSON Lines, default options | −4.71 to −0.98% | −6.03 to −0.68% | 2.03 / 1.85% |
| JSON Lines, heavy escaping | −1.80 to −0.50% | −1.82 to +0.13% | 0.13 / 1.20% |
| Short text report control | −0.74 to +3.48% | −0.52 to +3.25% | 0.38 / 3.86% |
| Quoted CSV invoice control | −2.93 to +3.81% | −3.22 to +5.42% | 4.22 / 2.61% |

Retain the traversal. All affected elapsed ranges fall, with the strongest
CPU support on default options. The controls span zero and leave adverse
pairings visible. Allocation is effectively flat, admitted peaks match and
each execution restores the allowance. A tiering-disabled Windows inspection
shows the eager entry and traversal together growing from 2,182 to 3,535
machine-code bytes. It establishes code shape under those settings.

Both platforms pass 221 white-box and 989 public focused Release checks on
the measured candidate, then 43 public checks each after refining one test.
The tests cover an opener delivered ahead of a default hook, dictionary
value replacement observed after a hook, in-flight cancellation and budget
reuse. An independent CPython 3.13.2 probe finds a pre-existing compatibility
gap. Compact and indented eager encoding observe key insertion and removal
in unsorted dictionaries. Lython reports a size change. Both engines use
pre-callback pairs for sorted encoding and reject those size changes in
incremental iteration. The original test and CPython observations are in
the procedures archive.

A follow-up CPython 3.13.2 probe deletes the item being encoded and observes
the next item at its original table position. Clearing the dictionary and
inserting `a` followed by `b` during the first item's callback produces
`{"first": "v", "b": 2}`. The native cursor resumes at the old physical
position in the new table. Lython compacts an ordered dictionary on removal.
An unchecked dense index would skip a live item after a deletion, and a cursor
over insertion sequence would visit `a` in the clear-and-replace case. Matching
these effects requires separate work on the dictionary representation.

The initial disk preflight refused staging before retiring any files or
starting workers. The retry reused the exact frozen reference and catalog
and retired the exact archived B1585 candidate copy. It retained the 2.5 MiB
collection margin and all sample and verification settings. All 140 batches,
both quiet gates, joins, fixture removal and archive verification pass.

[Timing and spread](summary.json) · [Allocation](allocation-summary.json) ·
[Images](identity.json) · [Reference core](../2026-11-24-json-ascii-runs/reference-core.dll.gz) ·
[Observations](observations.tar.xz) · [Procedures, oracle and JIT](procedures.tar.xz) ·
[Terminal receipt](terminal-status.json).
