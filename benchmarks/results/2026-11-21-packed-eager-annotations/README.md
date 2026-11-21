# B1290 packed eager annotation reads

The prototype replaces a group of constant and global annotation loads
with one operation that writes into the final annotation record. The
group admits that record and the callable together after decorators and
defaults, before the first annotation read. This can refuse earlier.
The defining operands own the unpublished record until the callable
takes it. General expressions keep their original suspension route.

The three eager fixtures each replace four read instructions with one,
and the defining helper's workspace drops from eight to six slots.
The postponed and fully supplied controls keep the same tape shape.
Local warm CLR allocation is effectively flat. All accounted peaks
match, and the shared allowance returns to baseline in every case.

Reference `256c5e651` and prototype `abf417f58` ran on the quiet Azure
F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 on November 21, 2026 UTC.
Each lane uses two independently warmed processes per source and seven
rotated batches per process. Ranges cover the four pairings of process
medians. Warm elapsed includes execution and prior-result disposal.
Time to result includes compilation and code-owner disposal as well.
Process CPU covers the wider request. Startup and final teardown are
outside these clocks.

| Complete script | Boundary | Elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | --- | ---: | ---: | ---: |
| Eager rules | warm | +13.12% to +14.79% | +12.89% to +13.57% | 0.56% / 0.91% |
| Eager nested factories | warm | −0.50% to +1.96% | −1.39% to +1.49% | 0.44% / 2.02% |
| Postponed nested factories | warm | +0.55% to +7.87% | +0.36% to +6.30% | 3.06% / 4.09% |
| Fully supplied helper | warm | −0.57% to +2.30% | −0.10% to +1.21% | 1.55% / 1.31% |
| Read every annotation dictionary | warm | +1.98% to +3.67% | +2.90% to +3.32% | 0.75% / 0.90% |
| Eager rules | compile-run | −3.48% to +0.15% | −3.33% to +0.00% | 3.58% / 0.17% |

Keep this implementation private. Eager rule execution is 13.12–14.79% slower and uses 12.89–13.57% more CPU across tight controls. Reading each annotation dictionary also costs more. Eager nested and eager time-to-result elapsed span zero. Postponed execution is adverse with wider duplicate-process spreads. The instruction and operand reduction did not produce a dependable elapsed benefit. The next prototype keeps the fused reads inside callable creation, removing the added opcode and slot marker.

Windows .NET 11 and Ubuntu .NET 10 each pass 363 white-box and 455
public Release checks. The new failed-read regression preserves the
name-error span and refunds the unpublished definition. Its first
local fixture set the alias on the wrong namespace. The next corrected
the namespace and exposed a CRLF offset mismatch in the test assertion.
The final fixture uses the defining namespace and normalized line
endings. The original test output is preserved.
Both complete frozen images match five CPython goldens. All 168 timed
batches and both quiet gates pass. Workers join and the persistent
archive verifies under the lease after guest fixtures are removed.

[Timing summary](summary.json) · [Compiler shape](annotation-shape.json) ·
[Local warm allocation](local-allocations.json) · [Raw observations](observations.tgz) ·
[Source, tests and procedures](checks-and-procedures.tar.xz).
