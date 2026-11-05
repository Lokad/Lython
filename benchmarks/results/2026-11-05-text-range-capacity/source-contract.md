# D53 bulk text range/capacity construction, before code

D52 current capture723text/1017ordinary samples, unknown7.88/8.75%, one truncated
map each; ordinary collector118390wakeups/1032allsamples. Dispatch21.72/12.29%
contains actual opcode work, not removable overhead. Ordinary call selection
5.67% text leaf/12.72% inclusive; no complete activation domination demonstrated.
Instructions already one compact opcode/span/five-int value; locals/operands
already share admitted backing. Naive merges or new argument/allowance arenas
remain unjustified; do not relabel B61/B69/D46 slower results as inconclusive.

Text SplitWhitespaceCore142/723inclusive samples, CollectWhitespaceForward27self
and AppendWhitespaceRange14self. Existing fused lower/upper split first decodes
every scalar to find ranges, then CaseOutputCapacity rescans each word to count
non-ASCII widths, then writes final words. This is actual repeated input work,
not a claimed19.64% removable share. Standalone casing legitimately sizes before
allocation; keep its capacity helper and current BCL/native Unicode writers.

Fold each word's conservative non-ASCII expansion allowance into the existing
forward range scan. Width1 stays unchanged; for width>1 add maximumCaseWidth-width
when casing is active. Capacity=end-start+extra is exactly the existing estimate,
including maximum full expansions. Whitespace breaks final-sigma context; output
writer/context rules are unchanged. Remove per-word sizing calls and source/casing
plumbing from AppendWhitespaceRange. Reverse and maxsplit remainder paths are
ordinary uncased splits only; remove unused casing from reverse collection.
No shared fragments, ASCII scanner, per-string fields, new ownership, caches,
specialized backend or mode. No change to guest string identity/alias funding.

Accept Python lower/upper+split for expansions, final sigma/combining marks,
all29 whitespace scalars, large ranges, uncased Unicode and bounded split/rsplit
controls. Preserve exact conservative capacity, checked representation bounds,
preflight before final storage, first refusal, cancellation and cleanup. Existing
focused tests plus small direct capacity/Unicode controls only where needed.

One<=180s controlled complete-text/CSV/JSON/report+Unicode comparison after focused
Release actual11/10; same frozen reference/current-control/CPython and previous
ownership/golden/quiet gates. Separate CPU/allocation/elapsed; no improvement
percentage veto. A bounded correction is allowed for demonstrated uncertainty,
retaining original conditions. Explicit retain/refine/withdraw decision. The
larger whole-script objective and full activation question remain open.
