# Rejected ASCII casing shortcut

The prototype was rejected and the runtime restored to immutable
`73deb8dcabb473673276c47ca8890dbd94a7b97a`. No fast path or test addition remains.
It mapped ASCII bytes directly inside the existing guarded casing loop, retaining
the governed builder and the Unicode fallback. ASCII timings improved, but the
Greek-only lowercase control failed the predeclared acceptance gate.

| Native 16-call sample | Baseline median | Prototype median | Allocated bytes, both |
| --- | ---: | ---: | ---: |
| ASCII lower, 4,096 bytes | 4.6046 ms | 1.68665 ms | 134,784 |
| ASCII upper, 4,096 bytes | 1.2987 ms | 0.6938 ms | 134,784 |
| Greek-only lower control, 8,192 bytes | 1.91315 ms | 6.1239 ms | 265,856 |
| Greek-only upper control, 8,192 bytes | 2.7659 ms | 2.703 ms | 265,856 |

The fixed gate required all eight candidate ASCII samples below every baseline
sample, unchanged allocation, and Greek-only medians no more than 10% higher.
The lower control failed. This is one short local diagnostic, not statistical
qualification or a whole-job/CPython/embedding-latency claim. The regression's
cause was not investigated; no JIT or layout attribution is made. There was no
repeat timing or relaxed gate. Do not revive this prototype from its ASCII gains.

Measured 2026-10-18 UTC on an Intel Core i7-14700KF, Windows 10.0.26200 x64,
Release net10.0. Baseline is the immutable revision above; prototype is the
[rejected patch](rejected.patch), including its ten small context test additions.
The [recipe](AsciiCasingDiagnostic.cs.txt) uses 16 warmups and eight samples of
16 transformations, with the ordinary execution-owned structural work checks
active. Input/context creation and owner disposal are outside measurement.
Each owner returns its allowance to zero. No requested GC, custom JIT settings,
unsafe code or protection changes. [Before](diagnostic-before.json),
[after](diagnostic-after.json), [comparison](comparison.json) and
[validation](validation.json) retain source, sample and receipt evidence.

The prototype passed 195 casing/Unicode/denial checks and exactly matched all
four [work-limit/fuel/refund records](work-records.json). These correctness results
did not override the performance rejection. Production and test sources were
restored, the temporary diagnostic removed, and the restored Release assembly
passed all 185 original focused cases. Existing runtime CI39106588147 remains
cached green at the baseline; the prototype was not platform/package qualified.
No public/accounting/regex contract changed. The authoritative performance
account remains [BENCHMARKS.md](../../../BENCHMARKS.md).
