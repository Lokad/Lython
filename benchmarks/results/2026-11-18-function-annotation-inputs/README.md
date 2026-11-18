# B1007 shared function annotation inputs

Measured `03bb07cf5` (`d97f6f98`) and prototype `8cfa31043` (`62665014`)
on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 on
2026-11-18 UTC. Both 32-file worker images verified. Five jobs took
229.70 seconds, with five measured rounds for each of two processes per
source. Both quiet gates and all golden checks passed. The workers joined
and the raw observations were archived and verified.

| Workload and boundary | Candidate elapsed change | Candidate process CPU change |
| --- | ---: | ---: |
| Annotated rule closures, warm | −9.90% to −7.11% | −11.78% to −6.54% |
| Annotated rule closures, compile-to-result | −8.06% to −6.56% | −8.67% to −4.85% |
| Annotated rule closures, compilation | −1.06% to +0.44% | −2.28% to +0.66% |
| Unannotated rule closures, warm | −1.32% to +0.50% | −2.57% to +2.00% |
| Positional calls, warm | −0.93% to +0.08% | −1.64% to +1.17% |

The affected report constructs annotated functions for 2,048 rules. Warm
maximum IQR is 4.51%. The paired elapsed and CPU results favor the candidate.
Both unchanged warm workloads span zero and compilation is inconclusive.
A separate Windows allocation screen shows 11.99% lower allocation for
annotated rules with the same accounted peak. All workloads return allowance
to baseline. The unannotated and positional control allocations are unchanged
in that screen.

The original yielding path evaluated positional-only annotations before regular
ones. CPython evaluates the regular group first. Prepare one ordered list during
lowering and use it for both evaluated operands and lowered annotations. This
removes runtime sorting, the separate yielding offset list and the references
to lowered default expressions in executable definition bindings. Both execution
modes match the independent CPython probe. Focused Windows .NET 11 and WSL
.NET 10 Release suites each passed 130 white-box and 414 public tests, including
postponed annotations, generic scopes, default ownership and the yielding
regression.

Warm includes output checks and prior-result cleanup. Compile-to-result also
includes compilation and compilation-owner disposal. Fixture reset, startup
and final cache teardown are outside elapsed. Process CPU includes wider
worker activity. The launcher retired only replicas already verified against
the archives recorded in the preflight.

[Measurements](summary.json) · [Raw observations](observations.tgz) ·
[Source, probes and procedures](checks-and-procedures.tar.xz)
