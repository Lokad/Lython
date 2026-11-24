# B1611 Deferred singleton default on a dedicated callable layout

Private `14b982071` keeps the prepaid slot only on callables that use it.
Other callable layouts keep their original size. Reading `__defaults__`
adopts the slot into one tuple under the original charge. Binding uses
the current default and preserves the identity of values selected before
a callback can replace it. The earlier general layout remains archived.
[B1610](../README.md).

The reference is `6c4287df3`. Both 32-file images differ only in the core
DLL. The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November
24, 2026 UTC. Two warmed workers per image supply seven rotated batches.
Ranges span all four pairings of worker medians. Warm elapsed includes
applicable mediated RAM-backed I/O, verification and previous-result
disposal. Compile to result adds compilation and compiled-owner disposal
in the running host. The wider CPU clock also includes request framing
and fixture reset.

| Complete script and boundary | Elapsed change | Process CPU change | Elapsed spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Captured rules, warm | −1.20 to +2.12% | −4.36 to +3.74% | 0.73 / 2.62% |
| Defaulted rules, warm | −3.16 to −2.75% | −4.85 to −4.54% | 0.24 / 0.19% |
| Eager annotated rules, warm | −5.37 to +5.29% | −5.99 to +6.23% | 8.48 / 2.56% |
| Postponed annotated rules, warm | +1.48 to +12.86% | 0.00 to +16.60% | 0.46 / 10.70% |
| Short Unicode report, warm | −6.19 to +0.80% | −7.17 to +0.85% | 5.39 / 1.96% |
| Quoted invoice, warm | −2.06 to +1.89% | −4.44 to +2.78% | 3.20 / 0.81% |
| Defaulted rules, compile to result | +1.62 to +3.26% | +0.55 to +3.94% | 0.80 / 0.81% |

Defaulted allocation falls from 978,264 to 781,656 bytes on the local
diagnostic, exactly 192 KiB or 20.1%. Allocation matches on captured, eager
and postponed rules. Admitted peaks match on all six scripts. Warm
defaulted elapsed and CPU fall in this block. Compile-to-result elapsed
and CPU rise on the same script with tight worker spreads. Postponed
elapsed is adverse and one candidate worker moves substantially. Keep
this layout private given the through-result cost and extra storage
variant. The timing and allocation observations are separate.

Windows and Ubuntu each pass 248 focused .NET 11 Release checks. Seven
independent programs agree with CPython 3.13.2 on both images in both
modes. They cover computed defaults, errors, identity, callable type,
hashing and shared metadata. All six worker goldens, 196 measured batches,
both quiet gates, joined cleanup and the persistent archive verify.

## Failed factory ownership

A separate probe exposes the same admission growth on both images in
both execution modes. Caught division by zero in the second member's
default raises execution peak from 2,432 bytes for one failure to 45,528
for 64. Failure in the first member peaks at 1,720 and 1,976 bytes.
The script can finish 256 attempts that fail in the first member under
a 96,000-byte shared allowance. The same allowance refuses when the
second is the member that fails. Both scripts finish under CPython.
The archived probe retains the earlier 65,536-byte condition, which
also refuses cases with a single failure. A separate JIT diagnostic of
the second-member error reaches `CompleteClosedDefinitionPair` on both
images in both execution modes. This rollback investigation uses the
shipped path as well as the deferred-slot prototype.

[Timings](summary.json) · [Images](identity.json) ·
[Allocation and admission](allocation-summary-six.json) · [Generated code](jit-summary.json) ·
[Raw observations](observations.tar.xz) · [Procedures, probes and patch](procedures.tar.xz) ·
[Recovered compressed storage](compaction.jsonl) · [Storage custody](storage-custody.json) ·
[Terminal status](terminal-status.json).
