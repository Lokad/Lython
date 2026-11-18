# Lexical binding and direct positional overflow delivery

GitHub Actions run `44370607146` passed at `2f98d36dbbb98d3bfc0e516dcff5a11bbcc1b882`.
Windows and Ubuntu passed Release on actual .NET 10 and .NET 11, including
the installed Release package consumer. The ordinary white-box and public
suites passed 4,876 and 18,069 cases per platform and framework. The
original-scale stress suites passed 14 and 406 cases.

Applying the archived lexical and direct variadic measurement patches to
`6c7ee7bd5` reconstructs the 589 production core files, tests and worker
sources at delivery. The comparison worker binaries and the CI artifacts
were compiled separately.

[CI run](https://github.com/Lokad/Lython/actions/runs/44370607146) ·
[Verified summaries](qualification.json) · [Source proof](source-proof.json) ·
[Official logs](run-logs.zip)
