# Platform and installed-package qualification

[CI run 44434897068](https://github.com/Lokad/Lython/actions/runs/44434897068)
passed at `edace442d` on 2026-11-19 UTC. This qualifies runtime
`6ce82ba48`, including direct function-definition completion
`934fe4a44`, fixed split operands and both refusal cases.

| Release check | Windows .NET 11 and .NET 10, each | Ubuntu .NET 11 and .NET 10, each |
| --- | ---: | ---: |
| Ordinary white-box | 4,895 passed | 4,895 passed |
| Ordinary public | 18,092 passed | 18,092 passed |
| Original white-box stress | 14 passed | 14 passed |
| Original public stress | 406 passed | 406 passed |
| Installed-package consumer | 0.11.0 passed | 0.11.0 passed |

[Workflow](workflow.json) · [Windows log excerpt](windows-verification.log) · [Ubuntu log excerpt](ubuntu-verification.log).
