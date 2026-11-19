# R587 platform and installed-package qualification

[CI run 44420753098](https://github.com/Lokad/Lython/actions/runs/44420753098)
passed on 2026-11-18 UTC. It tested `8b8f5d4e161e1e68246f5a69e0fd4faff564be63`,
which combines direct trailing keyword operands with the module annotation
mirror and admission-test corrections. The source and test trees are
unchanged at the B1016 comparison source `faeec7e81`.

| Runner, each on .NET 11 and .NET 10 | Ordinary white-box / public | Original stress white-box / public | Installed package |
| --- | ---: | ---: | --- |
| Windows | 4,897 / 18,090 | 14 / 406 | 0.11.0, compatibility and mediated file smoke passed |
| Ubuntu | 4,897 / 18,090 | 14 / 406 | 0.11.0, compatibility and mediated file smoke passed |

[Observed job status](workflow.json) · [Windows log excerpt](windows.txt) ·
[Ubuntu log excerpt](ubuntu.txt) ·
[Original failure and correction](../../2026-11-18-function-metadata-slots/delivery/README.md)
