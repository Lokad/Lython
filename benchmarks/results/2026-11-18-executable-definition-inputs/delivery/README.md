# Executable definition inputs Release qualification

CI `44405797706` passed at `2783d3d7f` on Windows and Ubuntu. Each
operating system passed 4,893 white-box and 18,081 public ordinary cases
per framework. Both .NET 10 and .NET 11 also passed 14 white-box and 406
public original-scale stress cases, plus the installed Release package checks.

The archived measurement patch matches the qualified source and tests.
The qualification establishes that those sources passed the Release gates.
The frozen benchmark image has its own recorded toolchain and identity.

[Actions run](https://github.com/Lokad/Lython/actions/runs/44405797706) ·
[Qualification](qualification.json) · [Logs](run-logs.zip) ·
[Source proof](source-proof.json) · [Measurement](../README.md)
