# Direct definition records and UTF-8 formatting delivery

The 2026-11-19 workflow dispatch passed on Windows and Ubuntu at
`5a0a1d37f`. The committed runtime and test inputs match `b491250cb`.
This checkpoint includes direct definition records (`e93356adc`) and
exact positional text formatting (`b491250cb`).
[CI 44453736908](https://github.com/Lokad/Lython/actions/runs/44453736908).

| Release check | Windows | Ubuntu |
| --- | --- | --- |
| .NET 11 ordinary suites | Passed | Passed |
| .NET 10 ordinary suites | Passed | Passed |
| Original stress scales on .NET 11 and .NET 10 | Passed | Passed |
| Installed Lokad.Lython 0.11.0 package consumer | Passed | Passed |

[Paired formatting checks](../README.md) ·
[Direct definition record checks](../../2026-11-19-direct-definition-records/README.md) ·
[Five-script CPython comparison](../../2026-11-19-retained-runtime-cpython/README.md).
