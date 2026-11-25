# B1730 Direct computed scalar arguments

The [warm CPU profiles](../2026-11-25-default-effects-cpu/README.md) found
call dispatch and direct scalar return work in both generated factories.
The prototype carries an exact arithmetic argument from the caller's
existing operands into the callee's existing direct return. It skips
temporary publication of the resolved function and argument on 160 sites
in each factory. Tape lengths, slot counts and admitted storage match.
The original instructions handle missing names, other callees, overflow,
protocols and suspension. Both managed stack checks remain in effect.

| Case | Lane | Elapsed difference | Process CPU difference | Same-source elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Complete ordinary definitions | Warm sync | −15.08% to −12.27% | −14.50% to −12.52% | 1.12% / 2.17% |
| Default effects diagnostic | Warm sync | −20.49% to −17.76% | −21.02% to −17.86% | 1.56% / 1.83% |
| Escaping defaulted rules | Warm sync | −0.73% to +4.79% | −0.75% to +5.02% | 5.33% / 0.22% |
| Complete ordinary definitions | Warm async | −15.92% to −14.78% | −14.87% to −13.10% | 0.96% / 0.40% |

The November 25, 2026 UTC quiet comparison on the Azure F4as_v7 uses Ubuntu
24.04.4 and .NET 11 RC3. Reference source is `857cc8001`. The candidate is
defined by the archived patch. The two 32-file frozen images differ only
in the core DLL. Three complete CPython golden checks pass per image.
Two independent processes per source warm for seven seconds each before
seven rotated batches each. The ranges span all four cross-source
process-median pairings. Negative values mean the candidate uses less
time. All workers join, the quiet gates pass and the collection is archived
under its lease. Elapsed covers warm execution, output verification and
result cleanup. Process CPU covers the wider request with framing. These
three scripts perform no file I/O during their warm timed executions.

The complete and diagnostic factories use less elapsed and CPU with
unchanged local CLR allocation and admitted memory, including compilation.
The escaping-rule control is inconclusive against its 5.33% reference
spread. Windows .NET 11 and Ubuntu .NET 10 each pass 282 focused Release
checks, including the protocol read that suspends after rebinding the
target of an earlier call. The initial preparation stopped before taking
the VM lease because a manifest was empty. The successful retry kept and
checked a prior verified manifest. It needed no replica retirement.

The prototype could route a subsequent augmented assignment around its
existing result fusion. The [retained confirmation](confirmation/README.md)
narrows eligibility to preserve that route and checks compilation and
compile-to-result separately.

[Identity](identity.json) · [Shape inspection](shape-summary.json) ·
[Execution allocation](allocation-summary.json) ·
[Compilation allocation](compile-allocation-summary.json) ·
[Timing summary](quiet-summary.json) · [Raw observations](quiet-observations.tar.xz) ·
[Terminal receipt](quiet-terminal-status.json) · [Procedures](procedures.tar.xz).
