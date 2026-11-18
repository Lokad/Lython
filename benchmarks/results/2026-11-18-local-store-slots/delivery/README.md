# CSV line numbers and consumed-slot delivery

GitHub Actions run `44346590882` passed at `b810f660ba79e2e491317d809c9b2fd3a504f536`.
Windows and Ubuntu passed Release on actual .NET 10 and .NET 11, including
original-scale stress and the installed Release package consumer. Per platform
and framework the ordinary white-box and public runs passed 4,871 and 18,055
cases, and stress passed 14 and 406.

Reconstructing the delivered source from the archived measurement patches
matches all 588 core production files and the exact committed tests and tools.
Qualification applies to those source and test inputs. The CI and benchmark
workers were built separately.

[CI run](https://github.com/Lokad/Lython/actions/runs/44346590882) ·
[Verified summaries](qualification.json) · [Source proof](source-proof.json) ·
[Official logs](run-logs.zip)
