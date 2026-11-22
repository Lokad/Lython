# B1352 Admit an immutable lexical keyword default with its callable

Compared `72e1131b9` with `ef49d04c7` on November 22, 2026 UTC.
Eligibility uses the existing binding collection and one parameter-write
flag. A proven initialized parameter read uses its existing operand.
The function and its inline keyword mapping backing share admission,
removing the prepared destination, the store instruction and a resize.
Dynamic defaults, eager annotations and mutable bindings retain their
usual staging. The later refusal tests add no production changes.

Both frozen 32-file images used the quiet Azure F4as_v7 with Ubuntu
24.04.4 and .NET 11 RC3. Two workers per revision each completed eight
seconds of full-request warmup, then provided seven rotated batches.
The ranges cover all process-median pairings. Warm includes output
verification and prior-result disposal. Time to result also includes
compilation and code-owner disposal. Process CPU covers the wider
request. These scripts use no file I/O. Startup and final teardown
are outside the measurement.

| Complete script | Phase | Elapsed change | Process CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Fully supplied helper | Warm | −5.29 to +4.48% | −4.16 to +6.49% | 8.85 / 1.34% |
| Eager nested factory | Warm | +0.41 to +1.63% | −0.41 to +2.55% | 0.70 / 0.51% |
| Mixed literal-default factory | Warm | −10.03 to −5.99% | −11.30 to −6.36% | 0.74 / 3.72% |
| Default-observing factory | Warm | +0.19 to +3.74% | −0.27 to +2.66% | 0.69 / 2.83% |
| Postponed nested factory | Warm | −3.69 to +0.74% | −3.52 to +0.17% | 1.49 / 3.06% |
| Mixed literal-default factory | Compilation | −0.98 to +0.85% | −2.13 to +1.34% | 1.03 / 0.81% |
| Mixed literal-default factory | Time to result | −5.60 to −2.61% | −6.95 to −1.75% | 1.49 / 1.66% |
| Eager nested factory, reversed order | Warm | −5.66 to +6.95% | −6.40 to +7.42% | 8.12 / 4.85% |
| Default-observing factory, reversed order | Warm | −1.21 to +0.58% | −2.09 to +0.83% | 1.50 / 0.31% |
| Mixed literal-default factory, reversed order | Time to result | −7.89 to −5.87% | −8.12 to −4.36% | 0.72 / 1.46% |

Retain the direct staging. Mixed time to result and CPU fall in both
collections. The observer's initial elapsed increase spans zero in
confirmation. The eager result stays uncertain because both controls
move considerably. Recheck eager and fully supplied scripts when the
shared callable constructor changes. Mixed compilation timing spans
zero, with 72 fewer local managed bytes per compilation. Warm allocation
and admitted peaks match on all five scripts and their budgets refund.

Windows .NET 11 and Ubuntu .NET 10 each pass 135 white-box and 360
public focused Release checks. Each platform also passes two targeted
sync and async tests for failed admission before publication, first
refusal and refund. Both frozen images match five CPython goldens.
All 280 timed batches and both sets of quiet gates pass. The collections
take 382 and 167 seconds. Workers join and both archives verify under
the lease after removing RAM-backed fixtures. An early Windows
subprocess fixture used the wrong local SDK path. The pinned runtime
rerun passes the same test.

[Initial timing](summary.json) · [Confirmation](confirmation/summary.json) ·
[Run allocation](allocation-summary.json) ·
[Compiler allocation](compile-allocation-summary.json) ·
[Initial observations](observations.tgz) ·
[Confirmation observations](confirmation/observations.tgz) ·
[Source and checks](checks-and-procedures.tar.xz).
