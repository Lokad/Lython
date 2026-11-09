# Compilation phase attribution

R128/B488, 2026-11-09, committed8472935f/coreac6b. Two existing complete scripts,
validated CSV helpers and config-edit, use the existing worker's compile-only lane.
Each has six seconds of warmup and about six seconds of CPU-clock sampling on the
quiet F4as_v7/Ubuntu24.04.4/.NET11RC3. Normal GC/tiering, diagnostic CLR perf maps.
Compilation includes prior-script disposal; guest execution and I/O are excluded.
Public goldens are verified before/after; Closed and zero-budget cleanup pass.

| Observed phase | Config (1,172 samples) | CSV helpers (1,199 samples) |
| --- | ---: | ---: |
| Parsing | 43.09% | 25.94% |
| Abstract analysis | 19.11% | 29.19% |
| Executable construction | 18.52% | 18.77% |
| Lexical preparation | 7.25% | 8.42% |
| Syntax validity | 2.90% | 5.34% |
| Lowering | 2.30% | 3.59% |
| Tokenization | 1.62% | 2.25% |

Each sample is assigned to its innermost observed phase. CLR map lookup also
attributes native/unknown leaves when an enclosing phase is visible. Leaf unknowns
remain 13.74%/15.93%; inlining, frame truncation and sampling limit precision.
These shares are useful work as well as overhead, not potential speedups. No elapsed
comparison or dominant single-phase claim follows. The small lowering share does
not justify a general compiler rewrite as the next performance investment.

Source inspection identifies duplicate string-literal staging within parsing:
token substring, decoded builder/string, second concatenation builder/string and
an unconditional formatted-parts list. F-string text constructs synthetic triple
delimiters to reuse decoding. R129 separately selects one source-to-literal decoder
and lazy concatenation, preserving current AST/analysis strings and runtime UTF-8
ownership. Other lexical walks need complete parent bindings before child scopes;
their presence alone is not evidence they can be fused safely.

All image/quiet/golden/cleanup gates pass. Actual compiler worker threads were
selected by recorded CPU use, rather than assuming compilation runs on a named
guest thread. All users ended; owned RAM was unmounted; raw traces/maps were archived
under the collection lease and verified locally. Terminal cached. No production
code changed for attribution. Publication remains postponed.

[Identity](identity.json), [selection](selection.md), [phase counts and traversal callers](phase-assessment.json),
[receipt](archive-receipt.json), [original raw evidence](observations.tgz),
[collection and offline analysis](preparation.tar.xz).
