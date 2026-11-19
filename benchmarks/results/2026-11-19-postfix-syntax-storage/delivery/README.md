# R591 postfix syntax storage qualification

The Windows and Ubuntu Release workflow passed at `785cf4b8b` on
2026-11-19 UTC. Its source and test trees match parser change `c6d4683e5`.
[CI run 44430587885](https://github.com/Lokad/Lython/actions/runs/44430587885).

| Runner, each on .NET 11 and .NET 10 | Ordinary white-box / public | Original stress white-box / public | Installed package |
| --- | ---: | ---: | --- |
| Windows | 4,895 / 18,090 | 14 / 406 | 0.11.0 compatibility and mediated file checks passed |
| Ubuntu | 4,895 / 18,090 | 14 / 406 | 0.11.0 compatibility and mediated file checks passed |

[Observed job status](workflow.json) · [Windows excerpt](windows.txt) ·
[Ubuntu excerpt](ubuntu.txt)
