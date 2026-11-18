# B990 keyword forwarding on the quiet VM

The exact [Windows screen](../README.md) images `087eebd6` and `40b2116e`
were measured on 2026-11-18 on the four-vCPU Azure F4as_v7 running Ubuntu
24.04.4 and .NET 11.0.0-rc.3.26522.148. Both images passed the comparison
catalog check. The runner rotated two independent workers per image through
eight seconds of warm-up and five measured rounds per lane. Quiet gates
passed before and after. All workers joined, the RAM-backed guest fixture mount
was removed, and the observation archive was verified under the exclusive
lease. Previously archived exact DLL replicas made room for the two cores.

| Workload and boundary | Elapsed change | Process CPU change | Reference same-source elapsed spread |
| --- | ---: | ---: | ---: |
| 1,024-row keyword forwarding report, warm | −10.24% to −5.57% | −9.52% to −6.15% | 4.55% |
| Keyword forwarding report, compile to result | −8.75% to −5.35% | −11.75% to −6.86% | 2.17% |
| Keyword forwarding report, compilation | −2.42% to +0.40% | −2.44% to +0.82% | 1.74% |
| Positional call control, warm | −2.59% to −0.99% | −3.86% to −1.32% | 0.78% |

Ranges are the four pairings of the two reference and two prototype worker
medians. Warm elapsed includes mediated RAM-backed file I/O, golden checks
and prior result disposal. Compile to result includes compilation and
compiled-owner disposal. Startup and fixture resets are outside elapsed.
Process CPU also captures worker activity outside those inner intervals.

The quiet run reverses both adverse local warm observations. Elapsed and
CPU improvements on the forwarding report exceed the favorable positional
control movement. Compilation remains inconclusive. The Windows measurements
remain adverse observations with no established cause. CPython was used for
catalog verification and has no measured B990 lane.

This is the original prototype binary. Review found that native keyword
insertion must preserve cancellable hashing for a fresh large name. The
subsequent corrected source is qualified and timed separately.

[Summary](summary.json) · [Raw observations](observations.tgz) · [Procedure](procedures.tar.xz)
