# Lython / CPython comparison

Complete collection; eligibility is assessed per case and session.

Lane: `fresh-process`. Revision: `9358c4e72f1a29e55703eee02b28ea404d01bc0a`. Policy/eligibility: 4/4. Receipt SHA-256: `165a684c52fb0b3398813b1be99ec85ddac71caee16aeff5777cbb36cf52438d`.

Scope: 14 selected cases from 14 manifest cases. Policy v4 limits each lane to ten minutes including retries. The quick profile covers twelve workloads and two controls, with one-second persistent warmup and seven pairs in each of three independent sessions; it is a limited baseline.

Ratio means CPython time / Lython time; values above one favor Lython. Intervals are fixed-seed paired-bootstrap 95% intervals within one session. Independent sessions are shown separately; there is no pooled interval or overall speedup.

Lython uses ordinary public limits and output projection; CPython uses -I -S, normal GC/GIL, and fresh namespaces without an equivalent in-process governor. Imports keep their source positions. Fresh-process samples include parent-observed owned launch through pipe drain and exit, with OS file caches retained. Invocation controls are visible and never subtracted.

The supervisor alone uses DOTNET_TieredCompilation=0 to avoid its background compiler contaminating idle checks; the override is removed before every worker launch. Both engines retain the recorded ordinary worker profiles.

| Workload | Category | Scale | Session | Status | Lython µs/job | CPython µs/job | CPython/Lython [95% interval] | Winner |
| --- | --- | --- | ---: | --- | ---: | ---: | --- | --- |
| control.empty.control | control | control | 1 | Control | 170361.457 | 29947.425 | — | — |
| control.empty.control | control | control | 2 | Control | 175523.944 | 29627.844 | — | — |
| control.empty.control | control | control | 3 | Control | 171345.749 | 29750.481 | — | — |
| control.tiny.control | control | control | 1 | Control | 278759.746 | 30750.161 | — | — |
| control.tiny.control | control | control | 2 | Control | 246181.111 | 28851.413 | — | — |
| control.tiny.control | control | control | 3 | Control | 257930.678 | 29427.935 | — | — |
| loops.integer.medium | core | medium | 1 | Unqualified | 296433.890 | 29527.792 | — | — |
| loops.integer.medium | core | medium | 2 | Unqualified | 300642.998 | 29512.676 | — | — |
| loops.integer.medium | core | medium | 3 | Unqualified | 314647.032 | 31310.810 | — | — |
| loops.integer.large | core | large | 1 | Unqualified | 314817.100 | 30297.556 | — | — |
| loops.integer.large | core | large | 2 | Unqualified | 313079.329 | 30069.159 | — | — |
| loops.integer.large | core | large | 3 | Unqualified | 319863.566 | 30315.253 | — | — |
| calls.keyword.medium | core | medium | 1 | Unqualified | 335613.628 | 29764.020 | — | — |
| calls.keyword.medium | core | medium | 2 | Unqualified | 346205.947 | 31034.841 | — | — |
| calls.keyword.medium | core | medium | 3 | Unqualified | 336773.116 | 30701.745 | — | — |
| lists.stable-sort.medium | core | medium | 1 | Unqualified | 345618.927 | 30959.836 | — | — |
| lists.stable-sort.medium | core | medium | 2 | Unqualified | 345827.234 | 30418.638 | — | — |
| lists.stable-sort.medium | core | medium | 3 | Unqualified | 347063.004 | 30438.026 | — | — |
| dicts.tuple-key-update.medium | core | medium | 1 | Unqualified | 410072.482 | 32226.181 | — | — |
| dicts.tuple-key-update.medium | core | medium | 2 | Unqualified | 390951.748 | 31871.010 | — | — |
| dicts.tuple-key-update.medium | core | medium | 3 | Unqualified | 383953.023 | 31007.988 | — | — |
| generators.drain.medium | core | medium | 1 | Unqualified | 324134.333 | 30540.611 | — | — |
| generators.drain.medium | core | medium | 2 | Unqualified | 323457.057 | 30153.446 | — | — |
| generators.drain.medium | core | medium | 3 | Unqualified | 349271.614 | 31815.791 | — | — |
| strings.scan-supplementary.medium | core | medium | 1 | Unqualified | 321887.396 | 30659.195 | — | — |
| strings.scan-supplementary.medium | core | medium | 2 | Unqualified | 315994.631 | 30466.674 | — | — |
| strings.scan-supplementary.medium | core | medium | 3 | Unqualified | 313414.509 | 30634.751 | — | — |
| strings.pipeline-ascii.medium | core | medium | 1 | Unqualified | — | — | — | — |
| json.transform-roundtrip.medium | library | medium | 1 | Unqualified | 343203.023 | 30671.892 | — | — |
| json.transform-roundtrip.medium | library | medium | 2 | Unqualified | 366900.241 | 31596.596 | — | — |
| json.transform-roundtrip.medium | library | medium | 3 | Unqualified | 335929.492 | 30032.240 | — | — |
| csv.retain.medium | library | medium | 1 | Unqualified | 315909.506 | 30541.664 | — | — |
| csv.retain.medium | library | medium | 2 | Unqualified | 313420.043 | 30580.328 | — | — |
| csv.retain.medium | library | medium | 3 | Unqualified | 313705.636 | 30690.775 | — | — |
| xml.parse-select.medium | library | medium | 1 | Unqualified | 358776.202 | 34309.713 | — | — |
| xml.parse-select.medium | library | medium | 2 | Unqualified | 339261.230 | 32248.558 | — | — |
| xml.parse-select.medium | library | medium | 3 | Unqualified | 334752.854 | 32507.615 | — | — |
| compression.zlib.medium | native-library | medium | 1 | Unqualified | 316728.749 | 31105.231 | — | — |
| compression.zlib.medium | native-library | medium | 2 | Unqualified | 318130.441 | 30660.696 | — | — |
| compression.zlib.medium | native-library | medium | 3 | Unqualified | 335691.645 | 32220.036 | — | — |

loops.integer.medium exclusions: Job is dominated by the invocation control in session 1.; Job is dominated by the invocation control in session 2.; Job is dominated by the invocation control in session 3..

loops.integer.large exclusions: Job is dominated by the invocation control in session 1.; Job is dominated by the invocation control in session 2.; Job is dominated by the invocation control in session 3..

calls.keyword.medium exclusions: Job is dominated by the invocation control in session 1.; Job is dominated by the invocation control in session 2.; Job is dominated by the invocation control in session 3..

lists.stable-sort.medium exclusions: Job is dominated by the invocation control in session 1.; Job is dominated by the invocation control in session 2.; Job is dominated by the invocation control in session 3..

dicts.tuple-key-update.medium exclusions: Job is dominated by the invocation control in session 1.; Job is dominated by the invocation control in session 2.; Job is dominated by the invocation control in session 3..

generators.drain.medium exclusions: Job is dominated by the invocation control in session 1.; Job is dominated by the invocation control in session 2.; Job is dominated by the invocation control in session 3..

strings.scan-supplementary.medium exclusions: Job is dominated by the invocation control in session 1.; Job is dominated by the invocation control in session 2.; Job is dominated by the invocation control in session 3..

strings.pipeline-ascii.medium exclusions: Three distinct complete independent sessions are required.; Independent worker process identities are missing or repeated.; At least one session fails semantic, timing, stability or evidence eligibility.; Job is dominated by the invocation control in session 1.; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs.; Invalid or missing per-invocation times..

json.transform-roundtrip.medium exclusions: Job is dominated by the invocation control in session 1.; Job is dominated by the invocation control in session 2.; Job is dominated by the invocation control in session 3..

csv.retain.medium exclusions: Job is dominated by the invocation control in session 1.; Job is dominated by the invocation control in session 2.; Job is dominated by the invocation control in session 3..

xml.parse-select.medium exclusions: Job is dominated by the invocation control in session 1.; Job is dominated by the invocation control in session 2.; Job is dominated by the invocation control in session 3..

compression.zlib.medium exclusions: Three distinct complete independent sessions are required.; At least one session fails semantic, timing, stability or evidence eligibility.; Job is dominated by the invocation control in session 1.; Job is dominated by the invocation control in session 2.; Job is dominated by the invocation control in session 3.; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs.; Pair order is not alternating near-balanced AB/BA..

Regex is excluded pending Utf8Regex qualification. Native-library jobs measure their complete applications, not interpreter dispatch alone. Allocation diagnostics are a separate profile; these timings contain no memory ratio.
