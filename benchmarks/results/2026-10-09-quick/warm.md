# Lython / CPython comparison

Complete collection; eligibility is assessed per case and session.

Lane: `warm`. Revision: `9358c4e72f1a29e55703eee02b28ea404d01bc0a`. Policy/eligibility: 4/4. Receipt SHA-256: `fe6b4385abf1e13ded93e28f7a31805f2bb7286200c55fdc66c8f85138cbbba5`.

Scope: 14 selected cases from 14 manifest cases. Policy v4 limits each lane to ten minutes including retries. The quick profile covers twelve workloads and two controls, with one-second persistent warmup and seven pairs in each of three independent sessions; it is a limited baseline.

Ratio means CPython time / Lython time; values above one favor Lython. Intervals are fixed-seed paired-bootstrap 95% intervals within one session. Independent sessions are shown separately; there is no pooled interval or overall speedup.

Lython uses ordinary public limits and output projection; CPython uses -I -S, normal GC/GIL, and fresh namespaces without an equivalent in-process governor. Imports keep their source positions. Fresh-process samples include parent-observed owned launch through pipe drain and exit, with OS file caches retained. Invocation controls are visible and never subtracted.

The supervisor alone uses DOTNET_TieredCompilation=0 to avoid its background compiler contaminating idle checks; the override is removed before every worker launch. Both engines retain the recorded ordinary worker profiles.

| Workload | Category | Scale | Session | Status | Lython µs/job | CPython µs/job | CPython/Lython [95% interval] | Winner |
| --- | --- | --- | ---: | --- | ---: | ---: | --- | --- |
| control.empty.control | control | control | 1 | Unqualified control | 88.576 | 1.588 | — | — |
| control.empty.control | control | control | 2 | Unqualified control | 80.895 | 1.484 | — | — |
| control.empty.control | control | control | 3 | Unqualified control | 82.128 | 1.492 | — | — |
| control.tiny.control | control | control | 1 | Control | 82.900 | 2.138 | — | — |
| control.tiny.control | control | control | 2 | Control | 81.634 | 2.122 | — | — |
| control.tiny.control | control | control | 3 | Control | 82.259 | 2.145 | — | — |
| loops.integer.medium | core | medium | 1 | Unqualified | 535.114 | 76.420 | — | — |
| loops.integer.medium | core | medium | 2 | Unqualified | 556.167 | 74.240 | — | — |
| loops.integer.medium | core | medium | 3 | Unqualified | 604.088 | 87.751 | — | — |
| loops.integer.large | core | large | 1 | Unqualified | — | — | — | — |
| calls.keyword.medium | core | medium | 1 | Unqualified | 1378.397 | 145.174 | — | — |
| calls.keyword.medium | core | medium | 2 | Unqualified | 1404.235 | 137.701 | — | — |
| calls.keyword.medium | core | medium | 3 | Unqualified | 1383.905 | 137.450 | — | — |
| lists.stable-sort.medium | core | medium | 1 | Unqualified | 7888.523 | 308.613 | — | — |
| lists.stable-sort.medium | core | medium | 2 | Unqualified | 5460.066 | 306.496 | — | — |
| lists.stable-sort.medium | core | medium | 3 | Unqualified | 5150.046 | 312.929 | — | — |
| dicts.tuple-key-update.medium | core | medium | 1 | Unqualified | 11770.104 | 609.113 | — | — |
| generators.drain.medium | core | medium | 1 | Unqualified | 1648.527 | 121.759 | — | — |
| generators.drain.medium | core | medium | 2 | Unqualified | — | — | — | — |
| strings.scan-supplementary.medium | core | medium | 1 | Unqualified | 1685.720 | 226.742 | — | — |
| strings.scan-supplementary.medium | core | medium | 2 | Unqualified | 1574.111 | 214.397 | — | — |
| strings.scan-supplementary.medium | core | medium | 3 | Unqualified | 1567.382 | 227.061 | — | — |
| strings.pipeline-ascii.medium | core | medium | 1 | Unqualified | 618.395 | 41.528 | — | — |
| json.transform-roundtrip.medium | library | medium | 1 | Unqualified | 673.525 | 61.704 | — | — |
| json.transform-roundtrip.medium | library | medium | 2 | Unqualified | 715.388 | 61.823 | — | — |
| json.transform-roundtrip.medium | library | medium | 3 | Unqualified | 741.407 | 61.032 | — | — |
| csv.retain.medium | library | medium | 1 | Unqualified | 1184.722 | 108.498 | — | — |
| csv.retain.medium | library | medium | 2 | Unqualified | 1176.735 | 107.213 | — | — |
| csv.retain.medium | library | medium | 3 | Unqualified | 1037.408 | 101.652 | — | — |
| xml.parse-select.medium | library | medium | 1 | Unqualified | 1513.318 | 68.687 | — | — |
| xml.parse-select.medium | library | medium | 2 | Unqualified | 1562.102 | 69.646 | — | — |
| xml.parse-select.medium | library | medium | 3 | Unqualified | 1795.048 | 68.915 | — | — |
| compression.zlib.medium | native-library | medium | 1 | Unqualified | 1760.372 | 178.022 | — | — |

control.empty.control exclusions: At least one session fails semantic, timing, stability or evidence eligibility.; Lane IQR/median exceeds ten percent.; Paired ratio interval spans more than fifteen percent..

loops.integer.medium exclusions: At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.empty.control; Job is dominated by the invocation control in session 1.; Job is dominated by the invocation control in session 2.; Job is dominated by the invocation control in session 3.; Lane IQR/median exceeds ten percent..

loops.integer.large exclusions: Three distinct complete independent sessions are required.; Independent worker process identities are missing or repeated.; At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.empty.control; Job is dominated by the invocation control in session 1.; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs.; Invalid or missing per-invocation times..

calls.keyword.medium exclusions: Invocation control did not qualify: control.empty.control.

lists.stable-sort.medium exclusions: At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.empty.control; Lane IQR/median exceeds ten percent.; AB/BA order changes the ratio by more than ten percent.; Paired ratio interval spans more than fifteen percent..

dicts.tuple-key-update.medium exclusions: Three distinct complete independent sessions are required.; Independent worker process identities are missing or repeated.; At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.empty.control; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs.; Lane IQR/median exceeds ten percent.; Pair order is not alternating near-balanced AB/BA.; Paired ratio interval spans more than fifteen percent..

generators.drain.medium exclusions: Three distinct complete independent sessions are required.; Independent worker process identities are missing or repeated.; At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.empty.control; Job is dominated by the invocation control in session 2.; Lane IQR/median exceeds ten percent.; AB/BA order changes the ratio by more than ten percent.; Paired ratio interval spans more than fifteen percent.; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs.; Invalid or missing per-invocation times..

strings.scan-supplementary.medium exclusions: Invocation control did not qualify: control.empty.control.

strings.pipeline-ascii.medium exclusions: Three distinct complete independent sessions are required.; Independent worker process identities are missing or repeated.; At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.empty.control; Job is dominated by the invocation control in session 1.; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs.; AB/BA order changes the ratio by more than ten percent..

json.transform-roundtrip.medium exclusions: At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.empty.control; Job is dominated by the invocation control in session 1.; Job is dominated by the invocation control in session 2.; Job is dominated by the invocation control in session 3.; Lane IQR/median exceeds ten percent.; Paired ratio interval spans more than fifteen percent..

csv.retain.medium exclusions: At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.empty.control; Lane IQR/median exceeds ten percent.; AB/BA order changes the ratio by more than ten percent.; Paired ratio interval spans more than fifteen percent..

xml.parse-select.medium exclusions: At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.empty.control; Lane IQR/median exceeds ten percent.; AB/BA order changes the ratio by more than ten percent.; Paired ratio interval spans more than fifteen percent..

compression.zlib.medium exclusions: Three distinct complete independent sessions are required.; Independent worker process identities are missing or repeated.; At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.empty.control; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs.; Pair order is not alternating near-balanced AB/BA..

Regex is excluded pending Utf8Regex qualification. Native-library jobs measure their complete applications, not interpreter dispatch alone. Allocation diagnostics are a separate profile; these timings contain no memory ratio.
