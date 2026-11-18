# Borrowed formatting and direct defaults Release qualification

CI run `44402133077` passed at `7fe127123` on Windows and Ubuntu. Each
operating system passed 4,889 white-box and 18,081 public ordinary tests
per target, with 14 white-box and 406 public original-scale stress tests
per target. Both installed Release package consumers passed. The logs
cover .NET 10 and .NET 11 and include all sixteen verified test summaries.

The runtime source matches the measured R574/R575 delivery at `53df08565`.
The sole test change replaces an assertion about a removed defaults map
with checks on live recursion, retained tuple funding and alias identity.
The original failures and focused rechecks remain with the measurement.

[Actions run](https://github.com/Lokad/Lython/actions/runs/44402133077) ·
[Qualification](qualification.json) · [Logs](run-logs.zip) ·
[Source proof](source-proof.json) · [Measurement](../README.md)
