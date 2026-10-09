# Lython / CPython comparison

Complete collection; eligibility is assessed per case and session.

Lane: `warm`. Revision: `56d4346d960b58f037da7d4590eb66c87ffec29e`. Policy/eligibility: 5/5. Receipt SHA-256: `d480b5283434bf8a09de67bd51011720859f0b468c86400df3f7491cb2e95ec7`.

Scope: 5 selected cases from 5 manifest cases. Policy v5 limits each lane to ten minutes including retries. The core-loop profile covers three geometric loop sizes and two controls; the quick profile covers twelve workloads and two controls. Persistent warmup lasts at least one second, with a recorded two-second preparation pause before sampling and seven pairs in each of three independent sessions.

Ratio means CPython time / Lython time; values above one favor Lython. Intervals are fixed-seed paired-bootstrap 95% intervals within one session. Independent sessions are shown separately; there is no pooled interval or overall speedup.

Lython uses ordinary public limits and output projection; CPython uses -I -S, normal GC/GIL, and fresh namespaces without an equivalent in-process governor. Imports keep their source positions. Fresh-process samples include parent-observed owned launch through pipe drain and exit, with OS file caches retained. Invocation controls are visible and never subtracted.

The supervisor alone uses DOTNET_TieredCompilation=0 to avoid its background compiler contaminating idle checks; the override is removed before every worker launch. Both engines retain the recorded ordinary worker profiles.

| Workload | Category | Scale | Session | Status | Lython µs/job | CPython µs/job | CPython/Lython [95% interval] | Winner |
| --- | --- | --- | ---: | --- | ---: | ---: | --- | --- |
| control.empty.control | control | control | 1 | Unqualified control | 82.768 | 1.522 | — | — |
| control.empty.control | control | control | 2 | Unqualified control | 83.667 | 1.614 | — | — |
| control.empty.control | control | control | 3 | Unqualified control | 90.287 | 1.640 | — | — |
| control.tiny.control | control | control | 1 | Control | 82.257 | 2.155 | — | — |
| control.tiny.control | control | control | 2 | Control | 85.035 | 2.139 | — | — |
| control.tiny.control | control | control | 3 | Control | 83.651 | 2.156 | — | — |
| loops.integer.small | core | small | 1 | Unqualified | 147.445 | 10.167 | — | — |
| loops.integer.small | core | small | 2 | Unqualified | 143.727 | 11.219 | — | — |
| loops.integer.small | core | small | 3 | Unqualified | 153.763 | 11.814 | — | — |
| loops.integer.medium | core | medium | 1 | Unqualified | 583.835 | 80.943 | — | — |
| loops.integer.medium | core | medium | 2 | Unqualified | 529.329 | 78.163 | — | — |
| loops.integer.medium | core | medium | 3 | Unqualified | 538.283 | 75.848 | — | — |
| loops.integer.large | core | large | 1 | Unqualified | 3624.931 | 665.725 | — | — |
| loops.integer.large | core | large | 2 | Unqualified | 3575.689 | 577.987 | — | — |
| loops.integer.large | core | large | 3 | Unqualified | 3719.754 | 583.478 | — | — |

control.empty.control exclusions: At least one session fails semantic, timing, stability or evidence eligibility.; Lane IQR/median exceeds ten percent..

loops.integer.small exclusions: Independent-session median ratios differ by more than ten percent.; Invocation control did not qualify: control.empty.control; Job is dominated by the invocation control in session 1.; Job is dominated by the invocation control in session 2.; Job is dominated by the invocation control in session 3..

loops.integer.medium exclusions: Three distinct complete independent sessions are required.; At least one session fails semantic, timing, stability or evidence eligibility.; Invocation control did not qualify: control.empty.control; Job is dominated by the invocation control in session 1.; Job is dominated by the invocation control in session 2.; Job is dominated by the invocation control in session 3.; Idle CPU exceeds three-percent median or five-percent maximum.; Expected 7 distinct indexed pairs..

loops.integer.large exclusions: Independent-session median ratios differ by more than ten percent.; Invocation control did not qualify: control.empty.control.

Regex is excluded pending Utf8Regex qualification. Native-library jobs measure their complete applications, not interpreter dispatch alone. Allocation diagnostics are a separate profile; these timings contain no memory ratio.
