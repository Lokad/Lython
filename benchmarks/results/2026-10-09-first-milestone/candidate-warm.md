# Lython / CPython comparison

Complete collection; eligibility is assessed per case and session.

Lane: `warm`. Revision: `4fb489d97783aab6cadc94387661f60378f689c5`. Policy/eligibility: 6/6. Receipt SHA-256: `1905db85faad501f45366f1d087231f0ed12b27f7fcd827832b7f1a0f1624f60`.

Scope: 14 selected cases from 14 manifest cases. Policy v6 limits each lane to ten minutes including retries. The core-loop profile covers three geometric loop sizes and two controls; the quick profile covers twelve workloads and two controls. Persistent warmup lasts at least one second, with a recorded two-second preparation pause before sampling and seven pairs in each of three independent sessions.

Ratio means CPython time / Lython time; values above one favor Lython. Intervals are fixed-seed paired-bootstrap 95% intervals within one session. Independent sessions are shown separately; there is no pooled interval or overall speedup.

Lython uses ordinary public limits and output projection; CPython uses -I -S, normal GC/GIL, and fresh namespaces without an equivalent in-process governor. Imports keep their source positions. Fresh-process samples include parent-observed owned launch through pipe drain and exit, with OS file caches retained. Invocation controls are visible and never subtracted.

Execution lanes require each engine's case median to exceed ten times its larger qualified control median.

The supervisor alone uses DOTNET_TieredCompilation=0 to avoid its background compiler contaminating idle checks; the override is removed before every worker launch. Both engines retain the recorded ordinary worker profiles.

| Workload | Category | Scale | Session | Status | Lython µs/job | CPython µs/job | CPython/Lython [95% interval] | Winner |
| --- | --- | --- | ---: | --- | ---: | ---: | --- | --- |
| control.empty.control | control | control | 1 | Control | 81.565 | 1.482 | — | — |
| control.empty.control | control | control | 2 | Control | 81.558 | 1.480 | — | — |
| control.empty.control | control | control | 3 | Control | 89.093 | 1.615 | — | — |
| control.tiny.control | control | control | 1 | Control | 89.547 | 2.321 | — | — |
| control.tiny.control | control | control | 2 | Control | 80.920 | 2.100 | — | — |
| control.tiny.control | control | control | 3 | Control | 84.051 | 2.165 | — | — |
| loops.integer.medium | core | medium | 1 | Unqualified | 356.181 | 74.316 | — | — |
| loops.integer.medium | core | medium | 2 | Unqualified | 414.857 | 77.405 | — | — |
| loops.integer.medium | core | medium | 3 | Unqualified | 356.107 | 77.556 | — | — |
| loops.integer.large | core | large | 1 | Unqualified | 2325.422 | 621.378 | — | — |
| loops.integer.large | core | large | 2 | Unqualified | 2372.411 | 615.300 | — | — |
| loops.integer.large | core | large | 3 | Unqualified | 2188.860 | 578.362 | — | — |
| calls.keyword.medium | core | medium | 1 | Unqualified | 1059.531 | 135.248 | — | — |
| calls.keyword.medium | core | medium | 2 | Unqualified | 1010.497 | 137.737 | — | — |
| calls.keyword.medium | core | medium | 3 | Unqualified | 1022.509 | 134.607 | — | — |
| lists.stable-sort.medium | core | medium | 1 | Unqualified | 5350.900 | 321.625 | — | — |
| lists.stable-sort.medium | core | medium | 2 | Unqualified | 5548.242 | 318.060 | — | — |
| lists.stable-sort.medium | core | medium | 3 | Unqualified | 5352.379 | 301.965 | — | — |
| dicts.tuple-key-update.medium | core | medium | 1 | Unqualified | 9831.297 | 572.621 | — | — |
| dicts.tuple-key-update.medium | core | medium | 2 | Unqualified | 9233.485 | 563.822 | — | — |
| generators.drain.medium | core | medium | 1 | Unqualified | 1559.908 | 111.250 | — | — |
| generators.drain.medium | core | medium | 2 | Unqualified | 1515.168 | 107.632 | — | — |
| generators.drain.medium | core | medium | 3 | Unqualified | 1624.006 | 115.381 | — | — |
| strings.scan-supplementary.medium | core | medium | 1 | Unqualified | 1115.964 | 217.179 | — | — |
| strings.scan-supplementary.medium | core | medium | 2 | Unqualified | 1120.225 | 223.469 | — | — |
| strings.scan-supplementary.medium | core | medium | 3 | Unqualified | 1128.513 | 221.278 | — | — |
| strings.pipeline-ascii.medium | core | medium | 1 | Qualified | 1202.978 | 41.878 | 0.035 [0.033, 0.035] | CPython |
| strings.pipeline-ascii.medium | core | medium | 2 | Qualified | 1282.079 | 41.538 | 0.032 [0.032, 0.034] | CPython |
| strings.pipeline-ascii.medium | core | medium | 3 | Qualified | 1363.770 | 44.900 | 0.033 [0.031, 0.034] | CPython |
| json.transform-roundtrip.medium | library | medium | 1 | Unqualified | 604.596 | 67.199 | — | — |
| json.transform-roundtrip.medium | library | medium | 2 | Unqualified | 640.595 | 63.108 | — | — |
| json.transform-roundtrip.medium | library | medium | 3 | Unqualified | 607.294 | 61.306 | — | — |
| csv.retain.medium | library | medium | 1 | Unqualified | 980.474 | 102.586 | — | — |
| csv.retain.medium | library | medium | 2 | Unqualified | 993.037 | 98.203 | — | — |
| csv.retain.medium | library | medium | 3 | Unqualified | 961.734 | 101.744 | — | — |
| xml.parse-select.medium | library | medium | 1 | Unqualified | 1444.785 | 72.668 | — | — |
| xml.parse-select.medium | library | medium | 2 | Unqualified | 1841.759 | 71.209 | — | — |
| xml.parse-select.medium | library | medium | 3 | Unqualified | 1435.733 | 68.825 | — | — |
| compression.zlib.medium | native-library | medium | 1 | Qualified | 1684.982 | 179.776 | 0.107 [0.107, 0.108] | CPython |
| compression.zlib.medium | native-library | medium | 2 | Qualified | 1619.373 | 175.428 | 0.108 [0.107, 0.109] | CPython |
| compression.zlib.medium | native-library | medium | 3 | Qualified | 1716.010 | 181.252 | 0.108 [0.107, 0.109] | CPython |

loops.integer.medium exclusions: Independent-session median ratios differ by more than ten percent.; Job is dominated by the invocation control in session 1.; Job is dominated by the invocation control in session 2.; Job is dominated by the invocation control in session 3..

loops.integer.large exclusions: At least one session fails semantic, timing, stability or evidence eligibility..

calls.keyword.medium exclusions: At least one session fails semantic, timing, stability or evidence eligibility..

lists.stable-sort.medium exclusions: At least one session fails semantic, timing, stability or evidence eligibility.; Lane IQR/median exceeds ten percent.; Paired ratio interval spans more than fifteen percent..

dicts.tuple-key-update.medium exclusions: Three distinct complete independent sessions are required.; Independent worker process identities are missing or repeated.; At least one session fails semantic, timing, stability or evidence eligibility.; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs.; Lane IQR/median exceeds ten percent.; Paired ratio interval spans more than fifteen percent..

generators.drain.medium exclusions: At least one session fails semantic, timing, stability or evidence eligibility.; Lane IQR/median exceeds ten percent.; Paired ratio interval spans more than fifteen percent..

strings.scan-supplementary.medium exclusions: At least one session fails semantic, timing, stability or evidence eligibility..

json.transform-roundtrip.medium exclusions: At least one session fails semantic, timing, stability or evidence eligibility.; Job is dominated by the invocation control in session 1.; Job is dominated by the invocation control in session 2.; Job is dominated by the invocation control in session 3.; Lane IQR/median exceeds ten percent.; Paired ratio interval spans more than fifteen percent.; AB/BA order changes the ratio by more than ten percent..

csv.retain.medium exclusions: At least one session fails semantic, timing, stability or evidence eligibility.; Lane IQR/median exceeds ten percent.; AB/BA order changes the ratio by more than ten percent.; Paired ratio interval spans more than fifteen percent..

xml.parse-select.medium exclusions: At least one session fails semantic, timing, stability or evidence eligibility.; Lane IQR/median exceeds ten percent.; AB/BA order changes the ratio by more than ten percent.; Paired ratio interval spans more than fifteen percent..

Regex is excluded pending Utf8Regex qualification. Native-library jobs measure their complete applications, not interpreter dispatch alone. Allocation diagnostics are a separate profile; these timings contain no memory ratio.
