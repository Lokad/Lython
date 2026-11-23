# B1489 Forward a live global and private local into one positional call

The change removes two adjacent load instructions from the existing call
shape. A live global remains the first read, followed by the local at its
original diagnostic span. An eligible native unary builtin consumes the
argument directly. Other callees use the existing call storage and can
transfer with the evaluated target and argument intact. The short report's
helper has nine instructions instead of 11 with the same five slots.
The rebound helper and other calls in the reports also lose adjacent loads.
No extra workspace, code table or alternate callable representation is kept.

The quiet Azure F4as_v7 used Ubuntu 24.04.4 and .NET 11 RC3 on November 23,
2026 UTC. Reference `53dcd2ac7` and measured `f9de5fd92` differ only in
the core DLL across both verified 32-file images. `2b501135a` retains the
measured production source. Two workers per source warm for eight seconds
and supply seven rotated batches. Ranges span the pairings of worker
medians. Warm elapsed includes applicable mediated RAM-backed I/O,
verification and prior-result disposal. Compilation and compiled-owner
cleanup are included in time to result. Process CPU also includes worker
reset and transport. Startup and final teardown sit outside elapsed.

| Workload | Phase | Elapsed change | Process CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Short nonlocal word report | Warm | −11.26 to −5.69% | −10.54 to −5.52% | 4.49 / 1.71% |
| Mixed-width word histogram | Warm | −0.48 to +3.51% | −1.48 to +4.05% | <0.01 / 4.01% |
| Fully supplied helper | Warm | −1.37 to +1.42% | −2.43 to +1.82% | 2.14 / 0.67% |
| Short report with rebound `len` | Warm | −2.62 to +1.11% | −2.81 to +1.31% | 1.06 / 2.74% |
| Short nonlocal word report | Compile to result | −2.39 to −1.95% | −3.13 to +1.50% | 0.25 / 0.21% |
| Quote-heavy invoice | Warm | −0.15 to +4.02% | 0.00 to +3.57% | 0.41 / 3.75% |

Retain the short report's measured warm improvement. The compile-to-result
elapsed direction is consistent and CPU spans zero. Both mixed-width text
and quoted invoice have moving candidate workers. Preserve those observations
and measure their debt in a shorter confirmation before attributing it to the
new call shape. Admission peaks match. Local short-report allocation matches
and the remaining differences are below 0.14%. All allowances return to
baseline. Windows JIT diagnostics preserve the expanded dispatch shapes and
show the same 344-byte Tier 1 stack subtraction in `NativeExecution.Call`.
Those diagnostics have a different execution boundary from the quiet timing.

Windows .NET 11 and Ubuntu .NET 10 each pass 1,069 white-box and 3,153
public focused Release checks. The first Windows test run compared a raw
CRLF offset with a normalized compilation span. The corrected test checks
line and column and passes the reported target-before-argument errors.
Pending file I/O tests check live rebinding, alias identity, cancellation
and refunds. The four new public cases also pass on unchanged production.
Both verified images match five independent CPython goldens and all
168 quiet timed batches in 281 seconds. The workers join and the archive
is verified under the lease after all guest fixtures are removed.

[Timings](summary.json) · [Identity](identity.json) · [Tape evidence](tape-summary.json) ·
[Allocation and admission](allocation-summary-six.json) · [JIT](jit-summary.json) ·
[Raw observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Source and procedures](procedures.tar.xz).
