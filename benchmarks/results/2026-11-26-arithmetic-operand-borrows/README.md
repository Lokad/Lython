# B1758 Borrow arithmetic operands from existing storage

This trial has the full and native executable prefixes borrow arithmetic
operands in source order. Integer and floating operations reuse the resolved
slots. Generic dispatch in the same frame receives canonical objects resolved
by that operation. Closure cells replace their separate object and integer
fields with the existing executable slot. The legacy floating operand reader
and separate integer-first dispatch disappear. Existing integer-run
specialization and protocol handoff stay in use.

| Workload | Data rows | Arithmetic source reads, reference → candidate | Whole-script global resolution calls |
| --- | ---: | ---: | ---: |
| Shipment callbacks | 2,048 | 38,944 → 20,512 | 4,264 each |
| Typed Unicode CSV meter | 4,096 | 45,056 → 32,768 | 4,102 each |
| Explicit-float CSV meter | 4,096 | 45,056 → 32,768 | 16,390 each |
| File-fed map | 2,048 | 0 → 0 | 2,052 each |

The dynamic route counts are from separately instrumented workers and
match in both hosting modes. An arithmetic read counts one source passed
through a numeric probe, ordinary input read or new borrow. These counts
describe the removed entry work and do not measure CPU. Both immutable
32-file images match four independent CPython output and file goldens.
Local Windows .NET 11 allocation and peak admission match closely. Peak
admission matches exactly. The only observed allocation movement is 212 B
on shipment. Allowance returns to baseline on all cases. Two older catalog
size fields say 1,024. CPython counts 4,096 data rows in each meter fixture.

| Workload | Warm lane | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Typed Unicode CSV meter | Sync | +4.21 to +5.82% | +2.19 to +4.69% |
| Shipment callbacks | Async | −0.11 to +2.99% | −2.52 to +3.48% |
| File-fed map control | Async | −1.13 to +1.00% | −2.87 to +3.42% |

The reference is `96f052932`, with the same runtime as `37df68bfe`.
The November 26, 2026 UTC run uses the quiet Azure F4as_v7 with Ubuntu
24.04.4 and .NET 11 RC3. Two processes per source warm for at least seven
seconds and supply seven rotated batches. Ranges span the four pairings of
worker medians. Warm elapsed includes mediated RAM-backed I/O, verification
and result disposal. Process CPU also includes framing and fixture reset.
All 84 batches, quiet gates, joins and archive verification pass.

Windows .NET 11 and Ubuntu .NET 10 each pass 601 white-box and 2,652 public
focused Release checks. Reflected addition observes the same captured integer
while a callback rebinds the cell and performs an asynchronous host read.

The meter slowdown is clear in this block. Keep the prototype and its
adverse result in the archive. The separate Windows JIT diagnostic emits
no direct calls to the borrower in the optimized Tier 1 arithmetic method.
The borrower also handles named sources and closure cells. Test whether
outlining those uncommon paths reduces the hot code before deciding whether
to retain the design. The diagnostic alone gives no CPU attribution.

[Timing](summary.json) · [Route counts](routes.json) · [Allocation and admission](allocation-summary.json) ·
[Identity](identity.json) · [Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures](procedures.tar.xz) · [JIT diagnostic](jit-diagnostic.tar.xz).
