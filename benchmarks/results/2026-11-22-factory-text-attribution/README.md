# B1422 Attribution after dictionary and frontend changes

Profiled the exact `3b45fdfc7` core on November 22, 2026 UTC. The
quiet Azure F4as_v7 runs Ubuntu 24.04.4 and .NET 11 RC3. Both
complete scripts use their verified B1419 workload goldens. Two
workers per script use the same core and original adapter. Each
warms through eight seconds of complete requests and runs about six
seconds under all-thread `perf` sampling at 199 Hz.

| Warm script | All samples | Dictionary accumulation | Casing preparation | Callable construction | Admission | Generic binder |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Nonlocal report | 1,204 / 1,212 | 151 / 162 | 154 / 134 | — | 77 / 53 | — |
| Postponed factories | 1,177 / 1,202 | — | — | 175 / 160 | 111 / 102 | 20 / 35 |

Counts use the nearest managed frame in each sample. Casing
preparation is the specific `PrepareCasedWhitespaceIteration` frame,
separate from samples nearest its downstream helpers. Of the two
factory workers' admission samples, 68 / 73 resolve to constructing
the callable and 38 / 28 to preparing default storage. Inlining and
unresolved frames limit causal attribution.

Construction and its admission still merit a complete storage or
transport redesign. The trace supplies little support for another
binder-only experiment. Both casing preparation and dictionary
accumulation remain visible after B1420. A new text design must
preserve complete preflight and removal of intermediate publications.
The profiles are diagnostic, with mediated I/O, output verification
and prior-result disposal. They supply no between-source or CPython
elapsed comparison.

An initial local Windows EventPipe profile verifies 158 batches.
Its factory trace coalesces most attributable thread time into
`StartNativeCall` and has intervals as large as 157.5 ms. The
quiet Linux collection provides the resolved method attribution
above. Both raw conditions are preserved.

All 40 Linux profile batches and eight follow-up batches verify.
Both quiet gates passed. All workers and collectors joined, guest
fixtures were removed, and the persistent archive verified under
the lease. The only retired VM working core was B1421's exact
candidate, already preserved with its archive.

[Profiles](summary.json) · [Frozen image](identity.json) ·
[Linux observations](observations.tgz) · [Windows evidence and procedures](procedures.tar.xz).
