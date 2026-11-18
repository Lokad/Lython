# B1004 executable function definition inputs

Measured the retained `53df08565` runtime (`6c4cb50a`) and prototype
`add5e4fe3` (`64fd18de`) on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and
.NET 11 RC3 on 2026-11-18 UTC. Both 32-file images verified. Five jobs took
230.59 seconds with two identical processes per source and five measured
rounds each. Exact output passed in every execution observation. Both quiet
gates passed, all workers joined and the evidence archive was verified.

| Script and boundary | Candidate elapsed change | Candidate process CPU change |
| --- | ---: | ---: |
| Rule closures, warm | −16.84% to −14.65% | −17.91% to −16.45% |
| Rule closures, compile-to-result | −15.08% to −14.24% | −14.56% to −13.92% |
| Rule closures, compilation | +2.60% to +4.54% | +2.26% to +4.65% |
| Reused defaults, warm | −3.35% to +2.70% | −3.01% to +3.33% |
| Positional calls, warm | −1.37% to +1.77% | −1.63% to +2.42% |

Warm and compile-to-result both improve on a report defining one function and
one lambda per input. Warm control spreads are 1.49% for the reference and
1.13% for the candidate, with maximum IQR 0.85%. Compilation adds about one
microsecond per report. The candidate shifts expression work into compilation
and retains both the compilation cost and the whole-request benefit. The
reused-default reference control spreads 3.80%, so it cannot resolve the
smaller adverse B1001 observation. A separate Windows allocation screen reports
4,829,056 bytes per report versus 5,336,984, down 9.52%, with the same 5,058,225
accounted peak. All catalog cases return the host allowance to baseline.

Executable definitions evaluate decorators and defaults as operands. One
construction step handles ordinary and yielding declarations. It replaces the
old definition opcode, decorator arrays and maps of captured expression shells.
The admitted default builder stays on the operand stack through suspension and
closes on exception, cancellation or generator abandonment. Generic type-scope
annotations and declarations in lowered consumers retain their evaluation paths.
Focused Windows .NET 11 and WSL .NET 10 Release checks passed. Independent
CPython output matched in both execution APIs for decorator/default/annotation
order, mutable default aliases, caller assignment and generator close.

Warm includes execution, mediated guest I/O, golden checks and prior-result
cleanup. Compile-to-result includes compilation and compiled-owner disposal.
Fixture reset, startup and final cache teardown are outside guest elapsed.
Process CPU includes the wider request. A read-only storage inventory timed
out before the run. The narrowed inventory found two verified aliases of an
already archived earlier core, which the launcher retired after a fresh check.

[Measurements](summary.json) · [Raw observations](observations.tgz) ·
[Source, tests and procedures](checks-and-procedures.tar.xz) ·
[Full Release qualification](delivery/README.md)
