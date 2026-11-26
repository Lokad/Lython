# B1748 Attribute the Unicode report control after direct affix returns

The exact reference `265e48ba2` and candidate `264a1176f` images from
[B1746/B1747](../2026-11-26-direct-affix-return/README.md) ran on the quiet
Azure F4as_v7 on November 26, 2026 UTC, with Ubuntu 24.04.4 and .NET 11 RC3.
Each image supplies one warmed, approximately six-second `cpu-clock` profile
at 199 Hz. The sampling window covers the whole repeated warm request,
including RAM-backed, mediated file I/O, result checks, cleanup and fixture
reset. Different invocation counts and one process per image make this an
attribution check. The paired confirmation remains the timing evidence.

| Observation | Reference | Candidate |
| --- | ---: | ---: |
| Completed requests during capture | 7,168 | 6,144 |
| All-thread samples | 1,281 | 1,248 |
| Stacks with resolved runtime ancestry | 1,168 | 1,153 |
| Unresolved leaves within those stacks | 113 | 111 |
| Word preparation or dictionary indexing ancestry | 463 | 455 |
| Nearest `TryFinishDirectCasedReturn` | 158 | 187 |
| Nearest `TryExecuteDictionaryLoop` | 184 | 175 |
| Nearest `PrepareCasedWhitespaceIteration` | 166 | 148 |
| Nearest `EnsureInterpreterStack` | 57 | 63 |

The exact compiled report instructions, operands, constants and call-site
metadata match across images. None of the module or two function tapes has
a direct affix return. `words_from_line` uses the existing direct cased
return. Both images already contain B1743's managed cursor checks. The
profiles point to the same main runtime paths and leave the cause of the
2.74–4.57% Unicode report CPU increase in confirmation unresolved. The
first adverse file-fed CSV result and its reversal also remain part of
B1746/B1747. Close this diagnostic and use the shared work it identifies
to guide source review.

Both 32-file images and the independent CPython output and file goldens
verified before collection. The collectors verified results before and after
capture and joined. Both quiet checks passed. The observation archive was
verified under the lease and the fixtures were removed. Staging reclaimed
one verified byte-identical core DLL from the aborted B1746 stage. The
original failure evidence and the verified counterpart remain available.

[Identity](identity.json) · [Attribution](attribution.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures and tape inspection](procedures.tar.xz).
