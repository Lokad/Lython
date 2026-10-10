# Lython / CPython comparison

Complete collection; eligibility is assessed per case and session.

Lane: `warm`. Revision: `39952af6e4c645c0733e6eda771cc513ecea16f0`. Policy/eligibility: 6/6. Receipt SHA-256: `be292b127cd0e902a08f7288c7c5bbe3ba4f45c5a02cd730cad8bf37fd552035`.

Scope: 14 selected cases from 14 manifest cases. Policy v6 limits each lane to ten minutes including retries. The core-loop profile covers three geometric loop sizes and two controls; the quick profile covers twelve workloads and two controls. Persistent warmup lasts at least one second, with a recorded two-second preparation pause before sampling and seven pairs in each of three independent sessions.

Ratio means CPython time / Lython time; values above one favor Lython. Intervals are fixed-seed paired-bootstrap 95% intervals within one session. Independent sessions are shown separately; there is no pooled interval or overall speedup.

Lython uses ordinary public limits and output projection; CPython uses -I -S, normal GC/GIL, and fresh namespaces without an equivalent in-process governor. Imports keep their source positions. Fresh-process samples include parent-observed owned launch through pipe drain and exit, with OS file caches retained. Invocation controls are visible and never subtracted.

Execution lanes require each engine's case median to exceed ten times its larger qualified control median.

The supervisor alone uses DOTNET_TieredCompilation=0 to avoid its background compiler contaminating idle checks; the override is removed before every worker launch. Both engines retain the recorded ordinary worker profiles.

| Workload | Category | Scale | Session | Status | Lython µs/job | CPython µs/job | CPython/Lython [95% interval] | Winner |
| --- | --- | --- | ---: | --- | ---: | ---: | --- | --- |
| control.empty.control | control | control | 1 | Control | 26.409 | 1.678 | — | — |
| control.empty.control | control | control | 2 | Control | 24.314 | 1.476 | — | — |
| control.empty.control | control | control | 3 | Control | 27.246 | 1.732 | — | — |
| control.tiny.control | control | control | 1 | Control | 25.149 | 2.130 | — | — |
| control.tiny.control | control | control | 2 | Control | 24.541 | 2.118 | — | — |
| control.tiny.control | control | control | 3 | Control | 24.469 | 2.125 | — | — |
| loops.integer.medium | core | medium | 1 | Unqualified | 231.273 | 75.373 | — | — |
| loops.integer.medium | core | medium | 2 | Unqualified | 226.154 | 74.191 | — | — |
| loops.integer.medium | core | medium | 3 | Unqualified | 226.876 | 83.120 | — | — |
| loops.integer.large | core | large | 1 | Qualified | 1665.986 | 636.466 | 0.383 [0.381, 0.387] | CPython |
| loops.integer.large | core | large | 2 | Qualified | 1613.375 | 575.583 | 0.357 [0.355, 0.359] | CPython |
| loops.integer.large | core | large | 3 | Qualified | 1658.726 | 595.714 | 0.359 [0.356, 0.361] | CPython |
| calls.keyword.medium | core | medium | 1 | Qualified | 863.653 | 135.778 | 0.158 [0.156, 0.159] | CPython |
| calls.keyword.medium | core | medium | 2 | Qualified | 880.801 | 146.427 | 0.166 [0.165, 0.167] | CPython |
| calls.keyword.medium | core | medium | 3 | Qualified | 836.757 | 135.960 | 0.163 [0.161, 0.164] | CPython |
| lists.stable-sort.medium | core | medium | 1 | Qualified | 4264.188 | 304.175 | 0.071 [0.068, 0.072] | CPython |
| lists.stable-sort.medium | core | medium | 2 | Qualified | 4307.902 | 301.578 | 0.070 [0.065, 0.071] | CPython |
| lists.stable-sort.medium | core | medium | 3 | Qualified | 4328.825 | 306.355 | 0.070 [0.065, 0.072] | CPython |
| dicts.tuple-key-update.medium | core | medium | 1 | Unqualified | 8448.118 | 570.651 | — | — |
| generators.drain.medium | core | medium | 1 | Unqualified | 1143.017 | 106.313 | — | — |
| generators.drain.medium | core | medium | 2 | Unqualified | 1154.964 | 107.560 | — | — |
| generators.drain.medium | core | medium | 3 | Unqualified | 1187.325 | 109.226 | — | — |
| strings.scan-supplementary.medium | core | medium | 1 | Unqualified | 885.407 | 220.028 | — | — |
| strings.scan-supplementary.medium | core | medium | 2 | Unqualified | 866.055 | 242.636 | — | — |
| strings.scan-supplementary.medium | core | medium | 3 | Unqualified | 906.031 | 217.186 | — | — |
| strings.pipeline-ascii.medium | core | medium | 1 | Unqualified | 140.934 | 41.491 | — | — |
| strings.pipeline-ascii.medium | core | medium | 2 | Unqualified | 138.031 | 41.239 | — | — |
| strings.pipeline-ascii.medium | core | medium | 3 | Unqualified | 139.538 | 41.177 | — | — |
| json.transform-roundtrip.medium | library | medium | 1 | Qualified | 426.802 | 62.003 | 0.145 [0.143, 0.148] | CPython |
| json.transform-roundtrip.medium | library | medium | 2 | Qualified | 407.146 | 60.956 | 0.150 [0.143, 0.161] | CPython |
| json.transform-roundtrip.medium | library | medium | 3 | Qualified | 388.707 | 62.229 | 0.158 [0.157, 0.161] | CPython |
| csv.retain.medium | library | medium | 1 | Unqualified | 510.743 | 99.269 | — | — |
| csv.retain.medium | library | medium | 2 | Unqualified | 495.484 | 99.162 | — | — |
| csv.retain.medium | library | medium | 3 | Unqualified | 464.650 | 99.768 | — | — |
| xml.parse-select.medium | library | medium | 1 | Unqualified | 734.212 | 69.817 | — | — |
| xml.parse-select.medium | library | medium | 2 | Unqualified | 764.593 | 69.893 | — | — |
| xml.parse-select.medium | library | medium | 3 | Unqualified | 776.500 | 69.098 | — | — |
| compression.zlib.medium | native-library | medium | 1 | Qualified | 1509.650 | 173.133 | 0.115 [0.114, 0.116] | CPython |
| compression.zlib.medium | native-library | medium | 2 | Qualified | 1523.128 | 173.560 | 0.114 [0.114, 0.115] | CPython |
| compression.zlib.medium | native-library | medium | 3 | Qualified | 1515.659 | 173.547 | 0.115 [0.114, 0.116] | CPython |

loops.integer.medium exclusions: At least one session fails semantic, timing, stability or evidence eligibility.; Job is dominated by the invocation control in session 1.; Job is dominated by the invocation control in session 2.; Job is dominated by the invocation control in session 3.; Lane IQR/median exceeds ten percent..

dicts.tuple-key-update.medium exclusions: Three distinct complete independent sessions are required.; Independent worker process identities are missing or repeated.; At least one session fails semantic, timing, stability or evidence eligibility.; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs..

generators.drain.medium exclusions: At least one session fails semantic, timing, stability or evidence eligibility.; Lane IQR/median exceeds ten percent.; AB/BA order changes the ratio by more than ten percent.; Paired ratio interval spans more than fifteen percent..

strings.scan-supplementary.medium exclusions: Independent-session median ratios differ by more than ten percent..

strings.pipeline-ascii.medium exclusions: Job is dominated by the invocation control in session 1.; Job is dominated by the invocation control in session 2.; Job is dominated by the invocation control in session 3..

csv.retain.medium exclusions: At least one session fails semantic, timing, stability or evidence eligibility.; Lane IQR/median exceeds ten percent.; AB/BA order changes the ratio by more than ten percent.; Paired ratio interval spans more than fifteen percent..

xml.parse-select.medium exclusions: At least one session fails semantic, timing, stability or evidence eligibility.; Lane IQR/median exceeds ten percent.; Paired ratio interval spans more than fifteen percent..

Regex is excluded pending Utf8Regex qualification. Native-library jobs measure their complete applications, not interpreter dispatch alone. Allocation diagnostics are a separate profile; these timings contain no memory ratio.
