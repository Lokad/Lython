# Lython / CPython comparison

Complete collection; eligibility is assessed per case and session.

Lane: `compile-run`. Revision: `9358c4e72f1a29e55703eee02b28ea404d01bc0a`. Policy/eligibility: 4/4. Receipt SHA-256: `161a909596867db4b3e2591afb834d804e0cfa5b8b83303e842deb9b2bbfcdcd`.

Scope: 14 selected cases from 14 manifest cases. Policy v4 limits each lane to ten minutes including retries. The quick profile covers twelve workloads and two controls, with one-second persistent warmup and seven pairs in each of three independent sessions; it is a limited baseline.

Ratio means CPython time / Lython time; values above one favor Lython. Intervals are fixed-seed paired-bootstrap 95% intervals within one session. Independent sessions are shown separately; there is no pooled interval or overall speedup.

Lython uses ordinary public limits and output projection; CPython uses -I -S, normal GC/GIL, and fresh namespaces without an equivalent in-process governor. Imports keep their source positions. Fresh-process samples include parent-observed owned launch through pipe drain and exit, with OS file caches retained. Invocation controls are visible and never subtracted.

The supervisor alone uses DOTNET_TieredCompilation=0 to avoid its background compiler contaminating idle checks; the override is removed before every worker launch. Both engines retain the recorded ordinary worker profiles.

| Workload | Category | Scale | Session | Status | Lython µs/job | CPython µs/job | CPython/Lython [95% interval] | Winner |
| --- | --- | --- | ---: | --- | ---: | ---: | --- | --- |
| control.empty.control | control | control | 1 | Unqualified control | 94.339 | 4.133 | — | — |
| control.empty.control | control | control | 2 | Unqualified control | 96.498 | 4.364 | — | — |
| control.empty.control | control | control | 3 | Unqualified control | — | — | — | — |
| control.tiny.control | control | control | 1 | Unqualified control | 103.977 | 6.629 | — | — |
| control.tiny.control | control | control | 2 | Unqualified control | 105.895 | 6.503 | — | — |
| control.tiny.control | control | control | 3 | Unqualified control | 102.538 | 6.682 | — | — |
| loops.integer.medium | core | medium | 1 | Unqualified | 596.876 | 92.965 | — | — |
| loops.integer.medium | core | medium | 2 | Unqualified | — | — | — | — |
| loops.integer.large | core | large | 1 | Unqualified | — | — | — | — |
| calls.keyword.medium | core | medium | 1 | Unqualified | — | — | — | — |
| lists.stable-sort.medium | core | medium | 1 | Unqualified | — | — | — | — |
| dicts.tuple-key-update.medium | core | medium | 1 | Unqualified | — | — | — | — |
| generators.drain.medium | core | medium | 1 | Unqualified | — | — | — | — |
| strings.scan-supplementary.medium | core | medium | 1 | Unqualified | — | — | — | — |
| strings.pipeline-ascii.medium | core | medium | 1 | Unqualified | 1296.894 | 73.089 | — | — |
| json.transform-roundtrip.medium | library | medium | 1 | Unqualified | — | — | — | — |
| csv.retain.medium | library | medium | 1 | Unqualified | 1179.905 | 139.308 | — | — |
| csv.retain.medium | library | medium | 2 | Unqualified | 1235.587 | 138.946 | — | — |
| csv.retain.medium | library | medium | 3 | Unqualified | 1378.686 | 138.378 | — | — |
| xml.parse-select.medium | library | medium | 1 | Unqualified | 2053.294 | 100.158 | — | — |
| xml.parse-select.medium | library | medium | 2 | Unqualified | — | — | — | — |
| compression.zlib.medium | native-library | medium | 1 | Unqualified | 2265.920 | 227.220 | — | — |

control.empty.control exclusions: Three distinct complete independent sessions are required.; At least one session fails semantic, timing, stability or evidence eligibility.; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs.; Invalid or missing per-invocation times..

control.tiny.control exclusions: At least one session fails semantic, timing, stability or evidence eligibility.; Lane IQR/median exceeds ten percent..

loops.integer.medium exclusions: Three distinct complete independent sessions are required.; Independent worker process identities are missing or repeated.; At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.empty.control; Invocation control did not qualify: control.tiny.control; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs.; Invalid or missing per-invocation times..

loops.integer.large exclusions: Three distinct complete independent sessions are required.; Independent worker process identities are missing or repeated.; At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.empty.control; Invocation control did not qualify: control.tiny.control; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs.; Invalid or missing per-invocation times..

calls.keyword.medium exclusions: Three distinct complete independent sessions are required.; Independent worker process identities are missing or repeated.; At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.empty.control; Invocation control did not qualify: control.tiny.control; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs.; Invalid or missing per-invocation times..

lists.stable-sort.medium exclusions: Three distinct complete independent sessions are required.; Independent worker process identities are missing or repeated.; At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.empty.control; Invocation control did not qualify: control.tiny.control; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs.; Invalid or missing per-invocation times..

dicts.tuple-key-update.medium exclusions: Three distinct complete independent sessions are required.; Independent worker process identities are missing or repeated.; At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.empty.control; Invocation control did not qualify: control.tiny.control; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs.; Invalid or missing per-invocation times..

generators.drain.medium exclusions: Three distinct complete independent sessions are required.; Independent worker process identities are missing or repeated.; At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.empty.control; Invocation control did not qualify: control.tiny.control; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs.; Invalid or missing per-invocation times..

strings.scan-supplementary.medium exclusions: Three distinct complete independent sessions are required.; Independent worker process identities are missing or repeated.; At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.empty.control; Invocation control did not qualify: control.tiny.control; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs.; Invalid or missing per-invocation times..

strings.pipeline-ascii.medium exclusions: Three distinct complete independent sessions are required.; Independent worker process identities are missing or repeated.; At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.empty.control; Invocation control did not qualify: control.tiny.control; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs.; Lane IQR/median exceeds ten percent.; AB/BA order changes the ratio by more than ten percent.; Paired ratio interval spans more than fifteen percent..

json.transform-roundtrip.medium exclusions: Three distinct complete independent sessions are required.; Independent worker process identities are missing or repeated.; At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.empty.control; Invocation control did not qualify: control.tiny.control; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs.; Invalid or missing per-invocation times..

csv.retain.medium exclusions: Three distinct complete independent sessions are required.; At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.empty.control; Invocation control did not qualify: control.tiny.control; Paired ratio interval spans more than fifteen percent.; Lane IQR/median exceeds ten percent.; AB/BA order changes the ratio by more than ten percent.; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs..

xml.parse-select.medium exclusions: Three distinct complete independent sessions are required.; Independent worker process identities are missing or repeated.; At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.empty.control; Invocation control did not qualify: control.tiny.control; Lane IQR/median exceeds ten percent.; AB/BA order changes the ratio by more than ten percent.; Paired ratio interval spans more than fifteen percent.; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs.; Invalid or missing per-invocation times..

compression.zlib.medium exclusions: Three distinct complete independent sessions are required.; Independent worker process identities are missing or repeated.; At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.empty.control; Invocation control did not qualify: control.tiny.control; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs.; AB/BA order changes the ratio by more than ten percent..

Regex is excluded pending Utf8Regex qualification. Native-library jobs measure their complete applications, not interpreter dispatch alone. Allocation diagnostics are a separate profile; these timings contain no memory ratio.
