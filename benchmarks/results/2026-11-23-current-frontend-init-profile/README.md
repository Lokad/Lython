# First-use compiler work after catalog separation

Seven Windows EventPipe traces use the retained `c6d2f2b3d` core
and the private first-use worker. Both file scripts verify their
stdout and output bytes before profiling. The empty control leaves
the known-call, callable-member and module initializers untouched.
[B1452](../2026-11-23-frontend-contract-sections/README.md) ·
[First-use worker](../2026-11-23-first-use-phases/README.md).

| Inclusive stack intervals | Configuration edit, range over three traces | Typed ASCII CSV, range over three traces |
| --- | ---: | ---: |
| Abstract interpreter | 68.2–72.0 ms | 83.4–113.9 ms |
| Known-call catalog initialization | 11.5–14.0 ms | 14.5–26.8 ms |
| Shared signature initialization inside known-call catalog | 2.0 ms | 3.5–10.0 ms |
| Callable-member catalog initialization | 10.5–12.2 ms | 11.0–13.5 ms |
| Module catalog initialization | 6.0–9.0 ms | 7.2–13.0 ms |

These are nested stack intervals from an instrumented development
machine. They include JIT and native time. The wider CSV ranges limit
attribution. They support testing removal of the all-names known-call
index, which constructs about 500 signatures and all diagnostic records
on the first qualifying lookup. The signature constructor accounts for
a smaller share than the enclosing index in the configuration edit.
Keep one runtime/analysis signature definition and demand it by module.
Check uncommon module and alias semantics, warm lookup cost and any
setup transferred to another script before retaining that design.

[Intervals and identities](summary.json) · [Raw traces and procedure](observations.tar.xz).
