# B1603 Seed captured binding arithmetic

Private `d6437c26f` feeds the known outer argument into the existing
arithmetic accumulator. It shares the arithmetic body and removes the
extra operand input used by B1602. The Windows FullOpts parameter and
capture evaluators return to 1,111 and 1,151 code bytes, respectively,
with 168-byte frames. The new binding evaluator is 842 code bytes
with a 128-byte frame.

| Complete script | Boundary | Elapsed change | Wider process CPU change | Elapsed spread reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Captured rules | Warm | −29.07 to −19.60% | −28.28 to −20.54% | 5.95 / 6.99% |
| Defaulted rules | Warm | −1.78 to +0.08% | −2.31 to +1.69% | 0.55 / 1.33% |
| Eager annotated rules | Warm | −1.69 to +3.66% | −0.42 to +3.71% | 3.29 / 2.09% |
| Postponed annotated rules | Warm | +2.58 to +7.56% | +2.46 to +7.89% | 1.89 / 2.90% |
| Short Unicode report | Warm | −3.59 to +4.01% | −0.14 to +3.91% | 3.93 / 3.81% |
| Quoted invoice | Warm | −0.64 to +2.10% | −1.35 to +2.60% | 1.94 / 0.81% |
| Captured rules | Compile to result | −31.68 to −27.18% | −32.64 to −26.97% | 6.20 / 0.35% |

The two 32-file images differ in the core DLL. The reference is the
same frozen `ad88474bb` runtime as B1602. The quiet Azure F4as_v7 ran
Ubuntu 24.04.4 and .NET 11 RC3 on November 24, 2026 UTC. Seven rotated
batches follow eight seconds of warmup per worker. Ranges cover four
pairings of independent process medians. Warm includes applicable
RAM-backed I/O, verification and previous-result disposal. Compile to
result also includes compilation and compiled-owner disposal. Wider
CPU covers the request. Startup and final teardown are separate.

Both platforms pass 106 focused Release checks. Six independent
CPython programs and all six script goldens verify on both images.
The seeded path leaves the compiled pair proof and retained ownership
unchanged. Captured allocation and all admitted peaks match, and shared
budgets return to baseline. All 196 timed batches, quiet gates, worker
joins, fixture cleanup and persistent archive verification pass.

Integrate the seeded evaluator for the restored shared evaluator,
smaller generated code, repeated captured improvement and through-result
gain. Warm worker spread limits the precision of the captured gain.
Postponed rules cost more on elapsed and CPU in this block, while the
first block had moving references. Retain that regression for the next
coherent call/frame comparison. The other four warm control ranges
cross zero, including quoted invoice.

[Timings](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Generated code](jit-summary.json) · [Images](identity.json) ·
[Raw observations](observations.tar.xz) · [Procedures and source](procedures.tar.xz) ·
[Storage custody](storage-custody.json) · [Terminal status](terminal-status.json) ·
[B1602 and reference core](../README.md).
