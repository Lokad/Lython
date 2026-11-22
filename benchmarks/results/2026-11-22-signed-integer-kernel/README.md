# B1360 Direct signed machine arithmetic

Compared `a206d2ab5` and `3c6a7cf23` on November 22, 2026 UTC on
the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. The
shared exact integer kernel uses operand signs for addition and
subtraction overflow and the managed signed high product for
multiplication. Overflow still follows the admitted BigInteger path.

| Script and phase | Elapsed change | Process CPU change | Same-source elapsed spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Arithmetic range loop, warm | −1.65…+0.47% | −2.34…0.00% | 1.61 / 0.54% |
| Fully supplied helper, warm | −2.02…−0.05% | −2.70…−0.82% | 1.95 / 0.06% |
| Typed ASCII CSV meter, warm control | −1.62…+2.88% | −0.94…+1.96% | 0.56 / 3.99% |
| Mixed factory, warm | −2.87…−0.30% | −3.36…−1.95% | 0.77 / 1.87% |
| Ordinary CSV invoice, warm | −11.40…+2.03% | −12.26…+1.90% | 13.51 / 1.45% |
| Quote-heavy policy invoice, warm | +0.15…+0.35% | −0.40…0.00% | 0.06 / 0.14% |
| Mixed factory, compile to result | −3.17…+1.07% | −2.65…+1.62% | 0.30 / 4.06% |

Negative means the candidate uses less time. Ranges cover all four
pairings of process medians. Warm includes applicable RAM-backed I/O,
golden verification and prior-result disposal. Time to result adds
compilation and code-owner disposal. Process CPU covers the wider
request. Startup and final teardown have separate boundaries.

The mixed and fully supplied factories use less warm CPU. Range-loop
elapsed and mixed time to result span zero. The ordinary invoice's
13.51% reference spread prevents attribution. The quote-heavy invoice
shows a small adverse elapsed range with no resolved CPU penalty.
Local allocation differs by at most 0.012%. All admitted peaks match
and every execution restores the allowance.

Windows x64 .NET 11 RC3 disassembly with tiering disabled shows 287
bytes in the candidate kernel, compared with 378 in the reference.
The multiply path uses one signed full-width multiply and compares the
high word with the sign extension. The reference uses three multiply
instructions and separate wide range checks. This code generation
check measures instruction shape. The quiet timings use normal tiering.

Windows .NET 11 and Ubuntu .NET 10 each pass 194 white-box and 292
public checks. These cover signed boundaries, promotion, alias
identity, division and modulo, protocol and cancellation unwind,
and terminal admission refusal. Both complete worker images match
six independent CPython goldens. All 196 timed batches verify in
384 seconds. Both quiet gates pass and every worker joins. Persistent
archive verification and fixture cleanup finish under the VM lease.

[Phase summary](summary.json) · [Raw observations](observations.tgz) ·
[Allocation](allocation-summary.json) · [Checks, code generation and source](checks-and-procedures.tar.xz) ·
[Prior continuation comparison](../2026-11-22-prepared-integer-continuations/README.md).
