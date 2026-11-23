# B1514 Project regex captures through an existing rune index

Private `3ef2b406e` borrows the subject's already admitted rune index to
translate capture endpoints. It removes ordered coordinate scratch and
UTF-8 scans for indexed subjects. ASCII and unindexed Unicode keep
their algorithms. Both images match five CPython 3.13.16 workload goldens.
Windows .NET 11 and Ubuntu .NET 10 each pass 197 focused Release checks,
including cancellation and provider cleanup. The independent capture
regression stays in **5b4d7de22** after 31 focused checks on each platform.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 23,
2026 UTC. Only the core DLL differs from retained `b7fdb4fec` across
the two verified 32-file images. Ranges pair two worker medians per
source after eight seconds of warmup. Warm elapsed uses compiled code
and includes verification and prior-result disposal. Wider CPU includes
worker resets and transport. Startup and final teardown sit outside elapsed.

| Complete script | Initial elapsed | Initial CPU | Confirmation elapsed | Confirmation CPU |
| --- | ---: | ---: | ---: | ---: |
| Indexed repeated interior windows | −1.54 to +0.77% | −2.33 to 0.00% | — | — |
| Indexed cursor capture spans | +3.21 to +4.15% | +3.70 to +6.10% | +2.67 to +4.78% | +2.38 to +4.88% |
| Unindexed Unicode cursor control | −0.80 to +1.66% | 0.00 to +1.20% | −3.41 to +1.58% | −4.68 to +1.16% |
| ASCII cursor control | −0.81 to +2.76% | 0.00 to +1.90% | — | — |
| Indexed Unicode wide registers | +9.80 to +14.20% | +10.50 to +16.20% | +8.28 to +10.15% | +7.91 to +10.48% |

The first collection verifies all 140 timed batches. Its final quiet
sample records 11.44% busy CPU, exceeding the existing idle gate. Treat
those timings as diagnostic. The repeat uses identical images, three
lanes and later status polling. Both quiet gates pass, all 84 batches
verify, and workers join. Both archives verify under their leases after
removal of owned guest fixtures.

Keep the projection variant private. The reduced scratch storage saves
80–816 admitted bytes on the three indexed scripts, with equal peaks
for both controls. The indexed window report also allocates 112,640
fewer CLR bytes, about 0.34%, in the local diagnostic. The confirmation
finds higher elapsed and CPU on both capture workloads. Retain the
existing coordinate projection and keep index borrowing for window
bounds. This comparison evaluates per-endpoint searches within the
existing index. It leaves other ways of displacing coordinate work open.

[Identity](identity.json) · [Confirmation](summary.json) ·
[Allocation](allocation-summary-six.json) · [Observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Initial diagnostics](initial-diagnostic-summary.json) ·
[Original observations](initial-observations.tar.xz) · [Original terminal status](initial-terminal-status.json) ·
[Checks and procedures](procedures.tar.xz).
