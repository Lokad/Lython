# Platform and installed-package qualification

[CI run 44444668048](https://github.com/Lokad/Lython/actions/runs/44444668048)
passed at `6db572c21` on 2026-11-19 UTC. It qualifies shared function
metadata keys `2409f0596` and common regex-window construction `41bfa18ca`.

| Release check | Windows .NET 11 and .NET 10, each | Ubuntu .NET 11 and .NET 10, each |
| --- | ---: | ---: |
| Ordinary white-box | 4,895 passed | 4,895 passed |
| Ordinary public | 18,095 passed | 18,095 passed |
| Original white-box stress | 14 passed | 14 passed |
| Original public stress | 406 passed | 406 passed |
| Installed-package consumer | 0.11.0 passed | 0.11.0 passed |

[Workflow](workflow.json) · [Windows log excerpt](windows-verification.log) · [Ubuntu log excerpt](ubuntu-verification.log).
