# R589 platform and installed-package qualification

On 2026-11-19 UTC, the full Windows and Ubuntu Release workflow passed at
`6c1da75b76a1f406210c035caf78841cf574e853`. Its source and test trees
match compiler change `33c690051`. [CI run 44424839322](https://github.com/Lokad/Lython/actions/runs/44424839322).

| Runner, each on .NET 11 and .NET 10 | Ordinary white-box / public | Original stress white-box / public | Installed package |
| --- | ---: | ---: | --- |
| Windows | 4,895 / 18,090 | 14 / 406 | 0.11.0, compatibility and mediated file smoke passed |
| Ubuntu | 4,895 / 18,090 | 14 / 406 | 0.11.0, compatibility and mediated file smoke passed |

[Observed job status](workflow.json) · [Windows log excerpt](windows.txt) ·
[Ubuntu log excerpt](ubuntu.txt)
