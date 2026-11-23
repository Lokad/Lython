# B1506 Borrow an owned rune index for regex cursor bounds

`00ca00e7d` retains the two-file change from `1dbaa59d2`. Single matches
and cursors use one UTF-8 window resolver. An interior bound on a
non-ASCII subject can use a rune index that the subject already owns.
ASCII and edge-only windows skip the lookup. Capture projection still
uses the existing coordinates and the provider receives the original
subject. Regex preparation allocates no index.

The comparison uses `b32bf6b6b` and the exact candidate against the same
optional PythonRe 0.4.0 adapter. Both 32-file images verify. The quiet
Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 23, 2026 UTC.
Each of two workers per source warms for eight seconds and supplies seven
rotated batches. Ranges pair their medians. Warm elapsed includes golden
verification and prior-result disposal. Compile to result adds compilation
and disposal of the compiled owner. Wider process CPU includes reset and
transport. Startup and final teardown sit outside elapsed.

| Workload | Phase | Elapsed change | Wider CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Indexed report with repeated interior bounds | Warm | −6.48 to −4.70% | −5.44 to −3.08% | 1.81 / 0.08% |
| Unindexed report with repeated interior bounds | Warm | −0.27 to +1.20% | 0.00% | 0.81 / 0.66% |
| Indexed cursor with capture spans | Warm | −0.97 to +2.35% | +1.19 to +3.69% | 1.92 / 1.41% |
| Unindexed Unicode cursor with capture spans | Warm | −0.43 to +0.82% | 0.00 to +1.32% | 1.12 / 0.13% |
| ASCII cursor with capture spans | Warm | −1.87 to −0.62% | −1.87% | 0.80 / 0.48% |
| Indexed report with repeated interior bounds | Compile to result | −7.34 to −4.40% | −9.89 to −5.44% | 2.04 / 1.11% |

Retain the shared resolver. Repeated interior bounds benefit in both
clock boundaries and the matched unindexed workload is flat in CPU.
The indexed single-window capture report uses more CPU with inconclusive
elapsed time. Keep it as a control if capture projection changes. Local
admitted peaks match across six workloads and every owner refunds to
baseline. Only the untimed single-search guard differs in allocation,
by 120 B per script invocation.

Windows .NET 11 and Ubuntu .NET 10 each pass 164 focused Release checks.
The new CPython example covers both cursor APIs, pre-window lookbehind,
bounded and reversed windows, scalar spans and original subject identity.
Six CPython workload goldens verify on both images. The initial manifest
omitted the collector's purpose field, causing exit before any timed work.
The corrected run reuses both unchanged images and retains the failed
record. All 168 timed batches pass in 288 seconds with both quiet gates,
joined workers, removed fixtures and archive verification under the lease.
A single historical core replica was retired after verifying its pushed
archive and full local copy.

[Identity](identity.json) · [Timing](summary.json) · [Allocation](allocation-summary-six.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Checks, failed stage and procedures](procedures.tar.xz) ·
[Exact reference core](../2026-11-23-eager-isolated-definition/README.md).
