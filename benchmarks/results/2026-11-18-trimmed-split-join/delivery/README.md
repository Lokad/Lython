# Direct trimmed split join delivery

GitHub Actions run `44352902445` passed at `6f494109f560b325988ae95ba65aaac3ffafcee6`.
Windows and Ubuntu passed Release on actual .NET 10 and .NET 11, including
original-scale stress and the installed Release package consumer. Per platform
and framework the ordinary white-box and public runs passed 4,871 and 18,063
cases. Stress passed 14 and 406 cases.

Applying the archived measurement patch to `b810f660b` reproduces all 589
core production files and the exact committed tests, tools and benchmark
worker source. CI and the benchmarks built separate binaries. This run
qualifies the measured split/trim/join source. The later line-count change
uses its own delivery checkpoint.

[CI run](https://github.com/Lokad/Lython/actions/runs/44352902445) ·
[Verified summaries](qualification.json) · [Source proof](source-proof.json) ·
[Official logs](run-logs.zip)
