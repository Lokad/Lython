# B1509 Skip the shared closed-return check for ordinary calls

Private `cd95f6ddc` guards B1507's unified return check with the existing
compiler facts in both callers. The Windows route diagnostic reaches
the general starter directly on the wide tuple script. Eager factories
and omitted integer defaults still finish through the shared check.

The comparison uses `a2bde38b2` and the exact candidate on the quiet
Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3, November 23, 2026 UTC.
Each of two independent workers per source warms for eight seconds and
supplies seven rotated batches. Ranges pair their medians. Warm elapsed
includes applicable mediated RAM-file I/O, golden verification and
prior-result disposal. Compile to result adds compilation and code-owner
disposal. Process CPU also includes fixture resets and protocol. Startup
and final teardown are outside elapsed.

| Workload | Boundary | Elapsed change | Wider CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Eager nested factories | Warm | −0.61 to +3.09% | −0.41 to +4.38% | 0.71 / 2.98% |
| Arithmetic with an omitted default | Warm | −3.03 to −1.78% | −7.26 to −2.13% | 0.07 / 1.22% |
| Fully supplied arithmetic | Warm | −3.21 to −0.03% | −1.96 to −0.33% | 2.23 / 1.03% |
| Wide native tuple helper | Warm | −3.59 to +5.11% | −4.04 to +1.30% | 4.70 / 4.13% |
| Quote-heavy invoice | Warm | −0.13 to +2.73% | +2.38 to +3.57% | 1.04 / 1.80% |
| Eager nested factories | Compile to result | −7.64 to −5.41% | −8.13 to −5.55% | 0.77 / 1.63% |

Keep the unified entry private. It improves omitted-default warm time
and eager first use with supporting CPU. Eager warm is inconclusive.
The wide call avoids the new check, with worker spread too high to
settle its elapsed effect. The complete quoted invoice uses more CPU
than the retained source, on top of the earlier measured control cost.
Keep the existing production boundary. A future design needs to avoid
added work on the supplied bound-call route. Compare the complete invoice
alongside factory and omitted-default workloads.

Windows .NET 11 and Ubuntu .NET 10 each pass 55 white-box and 107 public
focused Release checks. Both 32-file images verify five CPython workload
goldens. All admitted peaks match. Local allocations match except for
a six-byte difference in the quoted workload. The allowance returns to
baseline for every script. All 168 timed batches verify in 281 seconds.
Workers join and the persistent archive verifies under the quiet lease
after fixture removal. The first two staging attempts stop before timing.
The first retires one byte-verified, already archived working core and
has insufficient space at its original floor. The second stops on the
next replica proof. The successful stage adjusts the free-space margin
by one MiB with both benchmark images and sampling gates unchanged.

[Identity](identity.json) · [Timing](summary.json) · [Allocation](allocation-summary-six.json) ·
[JIT entry](jit-entry-summary.json) · [Observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Checks and procedures](procedures.tar.xz) ·
[Earlier variant and repeat](../2026-11-23-complete-native-closed-entry/README.md).
