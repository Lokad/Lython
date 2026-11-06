# R03 common UTF-8 word discovery, before implementation

HEAD0a64b922, production794e552b/Q01/R02/R04; no runtime prototype yet. B331
primary refresh327.85s/all gates retains large text gap and possible fresh CSV/
text regression debt. CI42170841788 is running on exact794e552b; no old terminal
query or unchanged qualification replay. Publication postponed.

Existing B324 whole-text profile has CollectCasedWhitespace73leaf samples, not a
predicted removable percentage. Source confirms two near-identical forward Rune
walks (ordinary and fused casing); reverse also decodes each ASCII character.
Output generation already uses native UTF-8 and bulk ASCII casing. JSON ordinary
strings/escaped strings/completed containers likewise already have direct native
paths; do not rediscover those as missing work or start another profiling campaign.

Hypothesis: replace per-character Rune decode/Unicode classification during ASCII
word/whitespace discovery with bounded native run searches. One common forward
planner handles ordinary maxsplit and optional existing case-capacity estimates;
delete CollectCasedWhitespace. A shared ASCII-run helper supports forward/reverse
search; Unicode scalars keep existing decoding/whitespace inventory. Fixed trusted
ASCII classification data is immutable metadata, not a guest cache or new policy.

Preserve the Python29-scalar whitespace inventory, including ASCII C0 separators
28–31 and non-whitespace NUL/DEL. Byte-string whitespace semantics remain separate.
Keep maxsplit0/negative/large remainders, reversed order, leading/trailing handling,
Unicode boundaries/final sigma/case expansion/ignorable neighbors, and current
custom-method/protocol paths. Existing64-byte discovery chunks and actual token
checks remain; a final Unicode scalar can cross a chunk by at most3bytes. No
checkpoint counters, ceilings, new representation, cache, factory or owner.

Retain complete-output and replacement/scratch admission before allocation,
independent final fragment funding, retained aliases, terminal refusal and awaited
cleanup. Output-capacity and casing/writing logic remain existing mechanisms.
More native-search work per short word is a possible cost; do not assume speedup.

Focused existing string/whitespace/casing/split semantic, ownership, refusal,
cancellation and real suspension regressions on both actual runtimes. Add only
independent chunk-transition controls not covered by existing4095–4097 cases,
validated against isolated CPython. Then short existing complete-text warm/fresh
and unchanged CSV comparison, two identical current roles, production reference,
normalGC/tiering/full goldens/quiet/lease/Closed/finalfunding/joins/fixtures,
three jobs~85s/owned130s. Preserve all adverse/uncertain outcomes and original
B331 conditions. Warm and fresh stay separate; no allocation/causalCPU prediction.
If unstable controls require it, bounded confirmation/refinement/measurement
correction is allowed, not a permanent closure rule or an open-ended campaign.

Retain only after assessing measured benefit and simplification versus any
remaining regression debt. Full platform/originalStress/installed checks batch at
a coherent boundary; work independently while CI runs. Core remains managed,
memory-safe, token-controlled and governed by borrowed shared ownership.
