# B1507–B1508 Complete existing closed returns before native setup

Private `103bfdb72` moves direct definition returns and exact arithmetic
with prepared defaults ahead of activation and optional tuple-unpack
setup. The ordinary interpreter and native prefix share the check for
all three closed return forms. The general starter keeps binding and
activation. The Windows route diagnostic leaves the starter uncompiled
on both factories and the omitted-default arithmetic script.

Both comparisons use `a2bde38b2` and the same 32-file candidate on the
quiet Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3, November 23, 2026 UTC.
Two independent workers per source warm for eight seconds and supply
seven rotated batches. Ranges pair their medians. Warm elapsed includes
applicable mediated RAM-file I/O, golden verification and prior-result
disposal. Compile to result adds compilation and code-owner disposal.
Process CPU also includes resets and transport. Startup and final
teardown are outside elapsed.

| Workload | Phase | Elapsed change | Wider CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Eager nested factories | Warm | −9.84 to −3.00% | −11.29 to −3.08% | 4.20 / 3.25% |
| Postponed nested factories | Warm | −6.11 to −1.10% | −8.69 to +0.14% | 1.69 / 3.58% |
| Arithmetic with an omitted default | Warm | −4.11 to −1.84% | −3.55 to −1.40% | 1.87 / 0.49% |
| Factories observing defaults | Warm | −4.73 to −0.14% | −1.43 to +2.08% | 2.00 / 2.76% |
| Fully supplied arithmetic | Warm | −1.83 to −0.74% | −0.91 to +0.46% | 0.85 / 0.26% |
| Wide native tuple helper | Warm | −1.56 to +7.28% | −3.80 to +8.24% | 2.06 / 6.78% |
| Quote-heavy invoice | Warm | −4.20 to +0.92% | −3.51 to +0.73% | 4.63 / 0.68% |
| Eager nested factories | Compile to result | −8.20 to −5.90% | −6.16 to −3.37% | 0.35 / 2.15% |
| Wide native tuple helper, B1508 | Warm | +0.55 to +1.81% | +1.06 to +3.31% | 1.24 / 0.01% |
| Quote-heavy invoice, B1508 | Warm | −2.80 to +1.37% | −1.10 to +2.50% | 4.25 / 0.03% |
| Eager nested factories, B1508 | Compile to result | −7.17 to +22.44% | −4.86 to +20.39% | 2.39 / 28.82% |

Keep the candidate private while refining the entry. Eager and omitted
warm gains have supporting CPU. Eager first-use magnitude is uncertain
after the repeat. The wide helper's repeat confirms higher elapsed and
CPU with stable candidate workers. Its tuple body has no closed return
and nevertheless enters the new check. Test skipping the check using
the existing body facts before ordinary calls enter it. Quoted elapsed
remains uncertain. Local allocations and admitted peaks match exactly
on all seven workloads, and every owner refunds to baseline.

Windows .NET 11 and Ubuntu .NET 10 each pass 428 white-box and 868 public
focused Release checks, plus all 47 stack and thread-context checks.
The first white-box build exposed fixtures using the previous internal
starter signature. Their assertions remain and the build failure is
archived. Both frozen images verify seven CPython workload goldens. All
224 initial and 84 repeat batches verify. Both leases end after quiet
gates, worker joins, fixture removal and persistent archive verification.

[Identity](identity.json) · [Initial timing](summary.json) · [Repeat timing](repeat-summary.json) ·
[Allocation](allocation-summary-six.json) · [JIT entry](jit-entry-summary.json) ·
[Initial observations](observations.tar.xz) · [Repeat observations](repeat-observations.tar.xz) ·
[Initial terminal status](terminal-status.json) · [Repeat terminal status](repeat-terminal-status.json) ·
[Checks and procedures](procedures.tar.xz) · [Exact reference core](../2026-11-23-supplied-scalar-return/README.md).
