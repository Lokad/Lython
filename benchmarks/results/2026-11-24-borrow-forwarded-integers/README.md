# B1631 Carry forwarded integers through the native call entry

The candidate lets both runners pass a live caller slot to the existing
native call entry. Closed arithmetic and definition pairs can use the
integer directly. Returning the argument itself publishes the caller's
canonical identity. General binding and continuations get a published
argument before copying it into owned storage. The separate forwarded
computed-definition entry is removed.

The local diagnostic allocates 43,680 fewer bytes for each of two
complete workloads. Defaulted rules fall from 978,264 to 934,584 B
(4.46%) and captured rules from 1,125,720 to 1,082,040 B (3.88%). Eager
rules differ by 120 B. The short Unicode report and quoted invoice match.
All five admission peaks match, and every allowance returns to baseline.

Windows and Ubuntu each pass 1,102 white-box and 782 public .NET 11
Release checks and 78 focused .NET 10 checks. Both images match five CPython 3.13.2 probes in both
execution modes. The probes cover direct return identity, exposed
defaults, wide integer promotion, dynamic operations and unpack failure.
All five complete-script goldens verify on both 32-file images.

## Controlled comparison

The reference is `0c907c166`. The quiet Azure F4as_v7 ran Ubuntu 24.04.4
and .NET 11 RC3 on November 24, 2026 UTC. Two independent workers per
image each warm for at least five seconds and supply seven rotated
batches per lane. Ranges span the four pairings of worker medians. Warm
elapsed includes applicable mediated RAM I/O, verification and prior-result
cleanup. Time to result adds compilation and compiled-owner disposal.
The wider CPU clock includes fixture reset and request framing. Startup
is measured separately.

| Workload and boundary | Elapsed change | Wider CPU change | Elapsed spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Defaulted rules, warm | −13.25 to −5.70% | −14.11 to −4.49% | 5.25 / 3.28% |
| Defaulted rules, time to result | −2.03 to +2.06% | −0.35 to +2.12% | 2.74 / 1.40% |
| Eager annotated rules, warm | −1.25 to +5.45% | −1.44 to +8.26% | 3.08 / 3.58% |
| Captured rules, warm | −8.18 to −2.24% | −8.02 to −4.09% | 4.06 / 2.31% |
| Short Unicode report, warm | −6.26 to +2.54% | −3.23 to +3.70% | 6.28 / 2.93% |
| Quoted CSV invoice, warm | −0.17 to +3.88% | −2.44 to +1.38% | 2.37 / 1.64% |

Retain the shared entry. Both affected factory workloads show lower warm
elapsed and CPU time. Defaulted time to result, eager rules and the
text and invoice controls remain inconclusive in this block. All 168
batches and both quiet gates pass. Workers join, fixtures are removed
and the archive verifies under the lease.

[Image identity](identity.json) · [Allocation](allocation-summary.json) ·
[Timings](quiet-summary.json) · [Observations](quiet-observations.tar.xz) ·
[Procedures, probes and source patch](procedures.tar.xz) · [Terminal receipt](quiet-terminal-status.json).
