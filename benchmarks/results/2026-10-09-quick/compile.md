# Lython / CPython comparison

Complete collection; eligibility is assessed per case and session.

Lane: `compile`. Revision: `9358c4e72f1a29e55703eee02b28ea404d01bc0a`. Policy/eligibility: 4/4. Receipt SHA-256: `f2ca5315980dbac89b017dd4928610e5f7fac8a66b6445487a8f38b5d86cf479`.

Scope: 14 selected cases from 14 manifest cases. Policy v4 limits each lane to ten minutes including retries. The quick profile covers twelve workloads and two controls, with one-second persistent warmup and seven pairs in each of three independent sessions; it is a limited baseline.

Ratio means CPython time / Lython time; values above one favor Lython. Intervals are fixed-seed paired-bootstrap 95% intervals within one session. Independent sessions are shown separately; there is no pooled interval or overall speedup.

Lython uses ordinary public limits and output projection; CPython uses -I -S, normal GC/GIL, and fresh namespaces without an equivalent in-process governor. Imports keep their source positions. Fresh-process samples include parent-observed owned launch through pipe drain and exit, with OS file caches retained. Invocation controls are visible and never subtracted.

The supervisor alone uses DOTNET_TieredCompilation=0 to avoid its background compiler contaminating idle checks; the override is removed before every worker launch. Both engines retain the recorded ordinary worker profiles.

| Workload | Category | Scale | Session | Status | Lython µs/job | CPython µs/job | CPython/Lython [95% interval] | Winner |
| --- | --- | --- | ---: | --- | ---: | ---: | --- | --- |
| control.empty.control | control | control | 1 | Unqualified control | 4.504 | 2.792 | — | — |
| control.tiny.control | control | control | 1 | Control | 9.210 | 4.377 | — | — |
| control.tiny.control | control | control | 2 | Control | 9.694 | 4.597 | — | — |
| control.tiny.control | control | control | 3 | Control | 9.681 | 4.699 | — | — |
| loops.integer.medium | core | medium | 1 | Unqualified | — | — | — | — |
| loops.integer.large | core | large | 1 | Unqualified | — | — | — | — |
| calls.keyword.medium | core | medium | 1 | Unqualified | 48.936 | 20.452 | — | — |
| calls.keyword.medium | core | medium | 2 | Unqualified | 48.121 | 19.982 | — | — |
| calls.keyword.medium | core | medium | 3 | Unqualified | — | — | — | — |
| lists.stable-sort.medium | core | medium | 1 | Unqualified | — | — | — | — |
| dicts.tuple-key-update.medium | core | medium | 1 | Unqualified | — | — | — | — |
| generators.drain.medium | core | medium | 1 | Unqualified | — | — | — | — |
| strings.scan-supplementary.medium | core | medium | 1 | Unqualified | 186.390 | 34.274 | — | — |
| strings.scan-supplementary.medium | core | medium | 2 | Unqualified | 165.159 | 35.128 | — | — |
| strings.scan-supplementary.medium | core | medium | 3 | Unqualified | — | — | — | — |
| strings.pipeline-ascii.medium | core | medium | 1 | Unqualified | 221.458 | 28.724 | — | — |
| strings.pipeline-ascii.medium | core | medium | 2 | Unqualified | — | — | — | — |
| json.transform-roundtrip.medium | library | medium | 1 | Unqualified | — | — | — | — |
| csv.retain.medium | library | medium | 1 | Unqualified | — | — | — | — |
| xml.parse-select.medium | library | medium | 1 | Unqualified | — | — | — | — |
| compression.zlib.medium | native-library | medium | 1 | Unqualified | — | — | — | — |

control.empty.control exclusions: Three distinct complete independent sessions are required.; Independent worker process identities are missing or repeated.; At least one session fails semantic, timing, stability or evidence eligibility.; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs.; Pair order is not alternating near-balanced AB/BA..

loops.integer.medium exclusions: Three distinct complete independent sessions are required.; Independent worker process identities are missing or repeated.; At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.empty.control; Job is dominated by the invocation control in session 1.; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs.; Invalid or missing per-invocation times..

loops.integer.large exclusions: Three distinct complete independent sessions are required.; Independent worker process identities are missing or repeated.; At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.empty.control; Job is dominated by the invocation control in session 1.; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs.; Invalid or missing per-invocation times..

calls.keyword.medium exclusions: Three distinct complete independent sessions are required.; At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.empty.control; Job is dominated by the invocation control in session 1.; Job is dominated by the invocation control in session 2.; Job is dominated by the invocation control in session 3.; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs.; Invalid or missing per-invocation times..

lists.stable-sort.medium exclusions: Three distinct complete independent sessions are required.; Independent worker process identities are missing or repeated.; At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.empty.control; Job is dominated by the invocation control in session 1.; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs.; Invalid or missing per-invocation times..

dicts.tuple-key-update.medium exclusions: Three distinct complete independent sessions are required.; Independent worker process identities are missing or repeated.; At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.empty.control; Job is dominated by the invocation control in session 1.; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs.; Invalid or missing per-invocation times..

generators.drain.medium exclusions: Three distinct complete independent sessions are required.; Independent worker process identities are missing or repeated.; At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.empty.control; Job is dominated by the invocation control in session 1.; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs.; Invalid or missing per-invocation times..

strings.scan-supplementary.medium exclusions: Three distinct complete independent sessions are required.; At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.empty.control; Job is dominated by the invocation control in session 1.; Job is dominated by the invocation control in session 2.; Job is dominated by the invocation control in session 3.; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs.; Invalid or missing per-invocation times..

strings.pipeline-ascii.medium exclusions: Three distinct complete independent sessions are required.; Independent worker process identities are missing or repeated.; At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.empty.control; Job is dominated by the invocation control in session 1.; Job is dominated by the invocation control in session 2.; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs.; Invalid or missing per-invocation times..

json.transform-roundtrip.medium exclusions: Three distinct complete independent sessions are required.; Independent worker process identities are missing or repeated.; At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.empty.control; Job is dominated by the invocation control in session 1.; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs.; Invalid or missing per-invocation times..

csv.retain.medium exclusions: Three distinct complete independent sessions are required.; Independent worker process identities are missing or repeated.; At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.empty.control; Job is dominated by the invocation control in session 1.; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs.; Invalid or missing per-invocation times..

xml.parse-select.medium exclusions: Three distinct complete independent sessions are required.; Independent worker process identities are missing or repeated.; At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.empty.control; Job is dominated by the invocation control in session 1.; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs.; Invalid or missing per-invocation times..

compression.zlib.medium exclusions: Three distinct complete independent sessions are required.; Independent worker process identities are missing or repeated.; At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.empty.control; Job is dominated by the invocation control in session 1.; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs.; Invalid or missing per-invocation times..

Regex is excluded pending Utf8Regex qualification. Native-library jobs measure their complete applications, not interpreter dispatch alone. Allocation diagnostics are a separate profile; these timings contain no memory ratio.
