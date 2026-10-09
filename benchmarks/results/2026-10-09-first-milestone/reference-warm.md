# Lython / CPython comparison

Complete collection; eligibility is assessed per case and session.

Lane: `warm`. Revision: `be8361779e59c0ce873f768bdbabb668f6100c55`. Policy/eligibility: 6/6. Receipt SHA-256: `0c1251e615f8475ee65002e307a5c2c0a45ec6d47f00ec730ec3f12672bcf0cd`.

Scope: 14 selected cases from 14 manifest cases. Policy v6 limits each lane to ten minutes including retries. The core-loop profile covers three geometric loop sizes and two controls; the quick profile covers twelve workloads and two controls. Persistent warmup lasts at least one second, with a recorded two-second preparation pause before sampling and seven pairs in each of three independent sessions.

Ratio means CPython time / Lython time; values above one favor Lython. Intervals are fixed-seed paired-bootstrap 95% intervals within one session. Independent sessions are shown separately; there is no pooled interval or overall speedup.

Lython uses ordinary public limits and output projection; CPython uses -I -S, normal GC/GIL, and fresh namespaces without an equivalent in-process governor. Imports keep their source positions. Fresh-process samples include parent-observed owned launch through pipe drain and exit, with OS file caches retained. Invocation controls are visible and never subtracted.

Execution lanes require each engine's case median to exceed ten times its larger qualified control median.

The supervisor alone uses DOTNET_TieredCompilation=0 to avoid its background compiler contaminating idle checks; the override is removed before every worker launch. Both engines retain the recorded ordinary worker profiles.

| Workload | Category | Scale | Session | Status | Lython µs/job | CPython µs/job | CPython/Lython [95% interval] | Winner |
| --- | --- | --- | ---: | --- | ---: | ---: | --- | --- |
| control.empty.control | control | control | 1 | Control | 80.802 | 1.488 | — | — |
| control.empty.control | control | control | 2 | Control | 81.147 | 1.501 | — | — |
| control.empty.control | control | control | 3 | Control | 81.496 | 1.495 | — | — |
| control.tiny.control | control | control | 1 | Control | 84.145 | 2.161 | — | — |
| control.tiny.control | control | control | 2 | Control | 83.244 | 2.152 | — | — |
| control.tiny.control | control | control | 3 | Control | 88.948 | 2.313 | — | — |
| loops.integer.medium | core | medium | 1 | Unqualified | 556.805 | 73.240 | — | — |
| loops.integer.medium | core | medium | 2 | Unqualified | 523.624 | 79.680 | — | — |
| loops.integer.medium | core | medium | 3 | Unqualified | 533.710 | 73.582 | — | — |
| loops.integer.large | core | large | 1 | Unqualified | 3601.406 | 576.764 | — | — |
| loops.integer.large | core | large | 2 | Unqualified | 3702.331 | 601.457 | — | — |
| loops.integer.large | core | large | 3 | Unqualified | 3874.198 | 775.874 | — | — |
| calls.keyword.medium | core | medium | 1 | Qualified | 1395.303 | 137.535 | 0.099 [0.098, 0.099] | CPython |
| calls.keyword.medium | core | medium | 2 | Qualified | 1322.962 | 138.914 | 0.104 [0.104, 0.105] | CPython |
| calls.keyword.medium | core | medium | 3 | Qualified | 1336.482 | 137.931 | 0.103 [0.103, 0.104] | CPython |
| lists.stable-sort.medium | core | medium | 1 | Unqualified | 4817.961 | 301.404 | — | — |
| dicts.tuple-key-update.medium | core | medium | 1 | Unqualified | 9683.581 | 565.054 | — | — |
| generators.drain.medium | core | medium | 1 | Unqualified | 1715.294 | 108.012 | — | — |
| generators.drain.medium | core | medium | 2 | Unqualified | 1936.499 | 118.288 | — | — |
| generators.drain.medium | core | medium | 3 | Unqualified | 1817.245 | 108.572 | — | — |
| strings.scan-supplementary.medium | core | medium | 1 | Unqualified | 1605.372 | 219.626 | — | — |
| strings.scan-supplementary.medium | core | medium | 2 | Unqualified | 1621.319 | 224.330 | — | — |
| strings.scan-supplementary.medium | core | medium | 3 | Unqualified | 1544.093 | 217.619 | — | — |
| strings.pipeline-ascii.medium | core | medium | 1 | Unqualified | 1198.862 | 41.380 | — | — |
| strings.pipeline-ascii.medium | core | medium | 2 | Unqualified | 1555.399 | 44.632 | — | — |
| strings.pipeline-ascii.medium | core | medium | 3 | Unqualified | 1321.588 | 43.423 | — | — |
| json.transform-roundtrip.medium | library | medium | 1 | Unqualified | 576.096 | 61.530 | — | — |
| json.transform-roundtrip.medium | library | medium | 2 | Unqualified | 563.716 | 66.063 | — | — |
| json.transform-roundtrip.medium | library | medium | 3 | Unqualified | 576.098 | 60.879 | — | — |
| csv.retain.medium | library | medium | 1 | Unqualified | 949.634 | 98.925 | — | — |
| csv.retain.medium | library | medium | 2 | Unqualified | 1045.029 | 107.125 | — | — |
| csv.retain.medium | library | medium | 3 | Unqualified | 996.408 | 103.918 | — | — |
| xml.parse-select.medium | library | medium | 1 | Unqualified | 1481.917 | 68.591 | — | — |
| xml.parse-select.medium | library | medium | 2 | Unqualified | 1467.106 | 68.468 | — | — |
| xml.parse-select.medium | library | medium | 3 | Unqualified | 1386.641 | 68.202 | — | — |
| compression.zlib.medium | native-library | medium | 1 | Unqualified | 1678.168 | 175.126 | — | — |
| compression.zlib.medium | native-library | medium | 2 | Unqualified | 1732.149 | 188.096 | — | — |
| compression.zlib.medium | native-library | medium | 3 | Unqualified | 1740.291 | 177.587 | — | — |

loops.integer.medium exclusions: Independent-session median ratios differ by more than ten percent.; Job is dominated by the invocation control in session 1.; Job is dominated by the invocation control in session 2.; Job is dominated by the invocation control in session 3..

loops.integer.large exclusions: At least one session fails semantic, timing, stability or evidence eligibility..

lists.stable-sort.medium exclusions: Three distinct complete independent sessions are required.; Independent worker process identities are missing or repeated.; At least one session fails semantic, timing, stability or evidence eligibility.; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs.; Pair order is not alternating near-balanced AB/BA.; AB/BA order changes the ratio by more than ten percent..

dicts.tuple-key-update.medium exclusions: Three distinct complete independent sessions are required.; Independent worker process identities are missing or repeated.; At least one session fails semantic, timing, stability or evidence eligibility.; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs.; Lane IQR/median exceeds ten percent.; AB/BA order changes the ratio by more than ten percent.; Paired ratio interval spans more than fifteen percent..

generators.drain.medium exclusions: At least one session fails semantic, timing, stability or evidence eligibility.; Lane IQR/median exceeds ten percent.; Paired ratio interval spans more than fifteen percent..

strings.scan-supplementary.medium exclusions: At least one session fails semantic, timing, stability or evidence eligibility..

strings.pipeline-ascii.medium exclusions: At least one session fails semantic, timing, stability or evidence eligibility..

json.transform-roundtrip.medium exclusions: At least one session fails semantic, timing, stability or evidence eligibility.; Job is dominated by the invocation control in session 1.; Job is dominated by the invocation control in session 2.; Job is dominated by the invocation control in session 3.; Lane IQR/median exceeds ten percent.; Paired ratio interval spans more than fifteen percent..

csv.retain.medium exclusions: At least one session fails semantic, timing, stability or evidence eligibility.; Lane IQR/median exceeds ten percent.; Paired ratio interval spans more than fifteen percent..

xml.parse-select.medium exclusions: At least one session fails semantic, timing, stability or evidence eligibility.; Lane IQR/median exceeds ten percent.; AB/BA order changes the ratio by more than ten percent.; Paired ratio interval spans more than fifteen percent..

compression.zlib.medium exclusions: At least one session fails semantic, timing, stability or evidence eligibility..

Regex is excluded pending Utf8Regex qualification. Native-library jobs measure their complete applications, not interpreter dispatch alone. Allocation diagnostics are a separate profile; these timings contain no memory ratio.
