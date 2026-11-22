# B1350 Use range enumerators directly in executable loops

Measured `3bbc7a416` against `12cecc6cf` on November 22, 2026 UTC.
Exact range loops keep the existing enumerator in the executable
operand. This drops the general cursor and the separate iterable
wrapper. The generic iteration path still uses the same range state.
Machine integers preserve exact promotion and existing observation
rules. Wide yields keep preflight and funding. Operand retirement
handles early exits and generator or async resumption.

Both frozen 32-file images use the same worker on the quiet Azure
F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3. Two workers per revision
complete eight seconds of full-request warmup and seven rotated
batches. Ranges span four pairings of process medians. Warm elapsed
includes verification and prior-result disposal. Time to result also
includes compilation and code-owner disposal. Process CPU covers the
wider request. The affected scripts use no guest file I/O. Startup
and final teardown are separate.

| Script | Phase | Elapsed change | Process CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Arithmetic range loop | Warm | −14.17 to −13.97% | −15.28 to −14.04% | 0.07 / 0.16% |
| 256 range materializations | Warm | −2.89 to +1.98% | −2.87 to +1.11% | 1.98 / 2.97% |
| Mixed factory | Warm | −2.64 to +4.49% | −2.82 to +4.59% | 3.27 / 3.92% |
| Fully supplied helper | Warm | −1.50 to −0.53% | −1.71 to −1.38% | 0.89 / 0.09% |
| Mixed factory, reversed order | Warm | −9.42 to −0.89% | −9.57 to −1.34% | 6.26 / 2.97% |
| Mixed factory, reversed order | Time to result | −7.12 to −2.20% | −7.20 to −2.50% | 1.58 / 3.66% |

Retain direct range iteration. The arithmetic loop gains about 14%
warm with tight controls. The initial factory measurements span zero.
Both lanes in the reversed-order confirmation favor the candidate,
with substantial spread in one unchanged warm control. Preserve the
uncertainty about the magnitude of the factory effect. Materialization
timing spans zero. Local managed allocation falls by 128 bytes in the
range loop and by 6,266 bytes for repeated materialization. Admitted
peaks match and all allowances refund.

Windows .NET 11 and Ubuntu .NET 10 each pass 431 white-box and 1,551
public focused Release checks. Both frozen images match four CPython
goldens. All 168 timed batches and both sets of quiet gates pass.
The initial collection takes 219 seconds and the confirmation takes
113 seconds. All workers join and both archives verify under the
lease after removing RAM-backed fixtures.

[Initial timing](summary.json) · [Confirmation](confirmation/summary.json) ·
[Allocation](allocation-summary.json) · [Raw initial observations](observations.tgz) ·
[Raw confirmation](confirmation/observations.tgz) · [Source and checks](checks-and-procedures.tar.xz).
