# Lython / CPython comparison

Complete collection; eligibility is assessed per case and session.

Lane: `warm`. Revision: `f90734ec1444c15f0815af0a232c49d71320f24f`. Policy/eligibility: 6/6. Receipt SHA-256: `4afc066710769013a4246ae8d24ae402668369ab846cb7ca24f292760dfc5e3a`.

Scope: 14 selected cases from 14 manifest cases. Policy v6 limits each lane to ten minutes including retries. The core-loop profile covers three geometric loop sizes and two controls; the quick profile covers twelve workloads and two controls. Persistent warmup lasts at least one second, with a recorded two-second preparation pause before sampling and seven pairs in each of three independent sessions.

Ratio means CPython time / Lython time; values above one favor Lython. Intervals are fixed-seed paired-bootstrap 95% intervals within one session. Independent sessions are shown separately; there is no pooled interval or overall speedup.

Lython uses ordinary public limits and output projection; CPython uses -I -S, normal GC/GIL, and fresh namespaces without an equivalent in-process governor. Imports keep their source positions. Fresh-process samples include parent-observed owned launch through pipe drain and exit, with OS file caches retained. Invocation controls are visible and never subtracted.

Execution lanes require each engine's case median to exceed ten times its larger qualified control median.

The supervisor alone uses DOTNET_TieredCompilation=0 to avoid its background compiler contaminating idle checks; the override is removed before every worker launch. Both engines retain the recorded ordinary worker profiles.

| Workload | Category | Scale | Session | Status | Lython µs/job | CPython µs/job | CPython/Lython [95% interval] | Winner |
| --- | --- | --- | ---: | --- | ---: | ---: | --- | --- |
| control.empty.control | control | control | 1 | Control | 86.129 | 1.561 | — | — |
| control.empty.control | control | control | 2 | Control | 92.160 | 1.736 | — | — |
| control.empty.control | control | control | 3 | Control | 85.323 | 1.559 | — | — |
| control.tiny.control | control | control | 1 | Unqualified control | 90.815 | 2.389 | — | — |
| control.tiny.control | control | control | 2 | Unqualified control | 88.408 | 2.260 | — | — |
| control.tiny.control | control | control | 3 | Unqualified control | 85.198 | 2.221 | — | — |
| loops.integer.medium | core | medium | 1 | Unqualified | 397.638 | 89.617 | — | — |
| loops.integer.medium | core | medium | 2 | Unqualified | 369.025 | 80.540 | — | — |
| loops.integer.medium | core | medium | 3 | Unqualified | 376.184 | 83.091 | — | — |
| loops.integer.large | core | large | 1 | Unqualified | 2309.498 | 632.292 | — | — |
| loops.integer.large | core | large | 2 | Unqualified | 2267.944 | 666.212 | — | — |
| loops.integer.large | core | large | 3 | Unqualified | 2557.152 | 714.406 | — | — |
| calls.keyword.medium | core | medium | 1 | Unqualified | 1065.209 | 145.204 | — | — |
| calls.keyword.medium | core | medium | 2 | Unqualified | 1106.769 | 153.911 | — | — |
| calls.keyword.medium | core | medium | 3 | Unqualified | 1083.648 | 147.903 | — | — |
| lists.stable-sort.medium | core | medium | 1 | Unqualified | 5643.264 | 319.923 | — | — |
| dicts.tuple-key-update.medium | core | medium | 1 | Unqualified | 11225.438 | 652.572 | — | — |
| generators.drain.medium | core | medium | 1 | Unqualified | 1865.797 | 114.641 | — | — |
| generators.drain.medium | core | medium | 2 | Unqualified | 1692.354 | 115.913 | — | — |
| generators.drain.medium | core | medium | 3 | Unqualified | 1835.102 | 118.834 | — | — |
| strings.scan-supplementary.medium | core | medium | 1 | Unqualified | 1190.540 | 231.432 | — | — |
| strings.scan-supplementary.medium | core | medium | 2 | Unqualified | 1185.111 | 235.001 | — | — |
| strings.scan-supplementary.medium | core | medium | 3 | Unqualified | 1171.570 | 224.464 | — | — |
| strings.pipeline-ascii.medium | core | medium | 1 | Unqualified | 1263.061 | 43.869 | — | — |
| strings.pipeline-ascii.medium | core | medium | 2 | Unqualified | 1473.105 | 47.227 | — | — |
| strings.pipeline-ascii.medium | core | medium | 3 | Unqualified | 1355.497 | 44.059 | — | — |
| json.transform-roundtrip.medium | library | medium | 1 | Unqualified | 618.932 | 61.741 | — | — |
| json.transform-roundtrip.medium | library | medium | 2 | Unqualified | 655.547 | 64.918 | — | — |
| json.transform-roundtrip.medium | library | medium | 3 | Unqualified | 606.938 | 61.328 | — | — |
| csv.retain.medium | library | medium | 1 | Unqualified | 1062.512 | 109.618 | — | — |
| csv.retain.medium | library | medium | 2 | Unqualified | 901.331 | 99.320 | — | — |
| csv.retain.medium | library | medium | 3 | Unqualified | 984.681 | 104.375 | — | — |
| xml.parse-select.medium | library | medium | 1 | Unqualified | 1457.777 | 74.927 | — | — |
| xml.parse-select.medium | library | medium | 2 | Unqualified | 1796.603 | 69.467 | — | — |
| xml.parse-select.medium | library | medium | 3 | Unqualified | 1639.754 | 79.884 | — | — |
| compression.zlib.medium | native-library | medium | 1 | Unqualified | 1610.109 | 174.268 | — | — |
| compression.zlib.medium | native-library | medium | 2 | Unqualified | 1781.898 | 191.866 | — | — |
| compression.zlib.medium | native-library | medium | 3 | Unqualified | 1618.322 | 174.931 | — | — |

control.tiny.control exclusions: At least one session fails semantic, timing, stability or evidence eligibility.; Lane IQR/median exceeds ten percent..

loops.integer.medium exclusions: Job is dominated by the invocation control in session 1.; Job is dominated by the invocation control in session 2.; Job is dominated by the invocation control in session 3.; Invocation control did not qualify: control.tiny.control.

loops.integer.large exclusions: Invocation control did not qualify: control.tiny.control.

calls.keyword.medium exclusions: Invocation control did not qualify: control.tiny.control.

lists.stable-sort.medium exclusions: Three distinct complete independent sessions are required.; Independent worker process identities are missing or repeated.; At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.tiny.control; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs.; Pair order is not alternating near-balanced AB/BA..

dicts.tuple-key-update.medium exclusions: Three distinct complete independent sessions are required.; Independent worker process identities are missing or repeated.; At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.tiny.control; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs.; AB/BA order changes the ratio by more than ten percent.; Paired ratio interval spans more than fifteen percent..

generators.drain.medium exclusions: At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.tiny.control; Lane IQR/median exceeds ten percent.; AB/BA order changes the ratio by more than ten percent.; Paired ratio interval spans more than fifteen percent..

strings.scan-supplementary.medium exclusions: Invocation control did not qualify: control.tiny.control.

strings.pipeline-ascii.medium exclusions: At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.tiny.control; Lane IQR/median exceeds ten percent.; AB/BA order changes the ratio by more than ten percent.; Paired ratio interval spans more than fifteen percent..

json.transform-roundtrip.medium exclusions: At least one session fails semantic, timing, stability or evidence eligibility.; Job is dominated by the invocation control in session 1.; Job is dominated by the invocation control in session 2.; Job is dominated by the invocation control in session 3.; Invocation control did not qualify: control.tiny.control; Paired ratio interval spans more than fifteen percent.; Lane IQR/median exceeds ten percent.; AB/BA order changes the ratio by more than ten percent..

csv.retain.medium exclusions: At least one session fails semantic, timing, stability or evidence eligibility.; Job is dominated by the invocation control in session 2.; Invocation control did not qualify: control.tiny.control; AB/BA order changes the ratio by more than ten percent.; Paired ratio interval spans more than fifteen percent.; Lane IQR/median exceeds ten percent..

xml.parse-select.medium exclusions: At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.tiny.control; Lane IQR/median exceeds ten percent.; AB/BA order changes the ratio by more than ten percent.; Paired ratio interval spans more than fifteen percent..

compression.zlib.medium exclusions: Invocation control did not qualify: control.tiny.control.

Regex is excluded pending Utf8Regex qualification. Native-library jobs measure their complete applications, not interpreter dispatch alone. Allocation diagnostics are a separate profile; these timings contain no memory ratio.
