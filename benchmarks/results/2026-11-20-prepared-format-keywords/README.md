# B1189 Prepared named format arguments

Ordinary named `str.format` calls borrow a keyword-to-operand index from
the compiled call site. This eliminates the invocation's scan, dictionary
allocation and temporary reservation. Compilation admits the index before
allocation. Expanded arguments use the general binder, and user-defined
methods keep their usual dispatch. The final prototype stores the index on
named sites only, resolving the unrelated retained-memory increase in the
initial prototype. Each measured named script retains 224 additional bytes
and allocates 264 additional bytes during compilation.

| Workload | Phase | Warm managed allocation change | Quiet elapsed change | Quiet process CPU change |
| --- | --- | ---: | ---: | ---: |
| Named formatting | Warm | −22.50% | −10.81% to −9.02% | −13.17% to −8.00% |
| Nested Unicode widths | Warm | −17.94% | −9.92% to −7.80% | −11.60% to −5.99% |
| File-backed named shipment, 3,072 records | Warm | −15.69% | −3.79% to −1.67% | −4.98% to −1.12% |
| File-backed named shipment, 3,072 records | Compile to result | | −6.19% to −5.37% | −6.94% to −4.88% |
| Positional shipment control | Warm | −0.003% | +0.12% to +2.45% | −0.80% to +2.34% |
| File-backed CSV invoice control | Warm | 0.00% | −1.26% to +0.64% | −2.56% to −1.32% |

The shipment variant uses named fields and produces the same output as
the positional script. The paired comparison holds source and fixture
constant within each row. Retained as `2e4cbacef`. The results support
improvements on the measured named workloads. The positional control's
adverse elapsed result and mixed CPU limit attribution outside those
workloads. Reference and candidate duplicate elapsed spreads there are
0.99% and 1.33%. The largest spread on a named lane is 1.92%. The initial
prototype and all control observations are archived.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 20,
2026 UTC. Exact source pair `ea7cfa8fe` and `f8f223df2`. Each process
warmed for at least 10 seconds. Five rotated rounds targeted half-second
requests. Ranges cover all four pairings of duplicate process medians.
Warm includes mediated I/O for the file-backed workloads, golden checks
and prior-result disposal. Compile to result adds compilation and ownership
cleanup. Fixture preparation and process startup are outside the clocks.

Windows .NET 11 and Ubuntu .NET 10 each passed 828 focused Release checks,
including format callbacks suspended across genuine host I/O, cancellation
and terminal memory refusal. Both 32-file images matched seven CPython
workload goldens. Five additional probes matched CPython in both execution
modes. The local diagnostics recovered the shared allowance in every case.
Accounted warm peaks fell by 51–128 bytes on the named workloads and matched
on all three controls. The quiet gates passed and all 120 timed batches
completed. Workers joined and the observations were verified under the VM
lease before fixture teardown.

[Summary](summary.json) · [Allocation](allocation-summary.json) ·
[Retained memory](retention-summary.json) · [Observations](observations.tgz) ·
[Checks and procedures](checks-and-procedures.tar.xz).
