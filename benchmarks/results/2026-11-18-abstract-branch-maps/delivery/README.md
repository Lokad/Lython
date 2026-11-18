# Frontend simplification delivery

Run 44339601577 passed at `e507b4667`. Both Windows and Ubuntu passed .NET 10
and .NET 11 ordinary Release suites, the original stress scales, and the
installed Release package consumer. Each platform and target passed 4,871
white-box and 18,054 public ordinary tests, plus 14 white-box and 406 public
stress tests. All 16 test summaries were verified against the full logs.

Applying the two archived measurement patches to the qualified `c3d21ab45`
sources reproduces the exact delivered source, tools and tests, including
all 588 core production files. This qualifies the scope-directive and
branch-transfer changes at their measured sources. Their archived timing
images identify the measured binaries.

[Green run](https://github.com/Lokad/Lython/actions/runs/44339601577) ·
[Qualification](qualification.json) · [Source proof](source-proof.json) ·
[CI logs](run-logs.zip) · [Scope traversal](../../2026-11-18-directive-presence/README.md) ·
[Branch maps](../README.md)
