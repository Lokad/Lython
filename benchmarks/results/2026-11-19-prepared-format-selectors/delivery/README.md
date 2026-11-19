# Platform and installed-package qualification

[CI run 44440557042](https://github.com/Lokad/Lython/actions/runs/44440557042)
passed at `cc5ff3e3f` on 2026-11-19 UTC. This qualifies prepared format
positions and selectors at runtime revision `b8532e0ef`.

| Release check | Windows .NET 11 and .NET 10, each | Ubuntu .NET 11 and .NET 10, each |
| --- | ---: | ---: |
| Ordinary white-box | 4,895 passed | 4,895 passed |
| Ordinary public | 18,093 passed | 18,093 passed |
| Original white-box stress | 14 passed | 14 passed |
| Original public stress | 406 passed | 406 passed |
| Installed-package consumer | 0.11.0 passed | 0.11.0 passed |

[Workflow](workflow.json) · [Windows log excerpt](windows-verification.log) · [Ubuntu log excerpt](ubuntu-verification.log).
