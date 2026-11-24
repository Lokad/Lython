# B1626 Share regex operation dispatch

Module functions resolve the pattern before executing the requested
operation. They and compiled methods share numeric coercion, subject
checks and matcher dispatch. The module input readers, their three
intermediate records and the second dispatch switch are removed. The
core change removes 200 net lines and adds no provider dependency.

Explicit `None` for `count` or `maxsplit` raises `TypeError` at runtime.
Omitted counts retain their defaults. Lython's window extensions stay
available, including omitted module window bounds expressed as
`None`. Replacements are checked even on a miss or a negative count.
An invalid template keeps its error ahead of an invalid subject.
The pattern cache, provider selection and compilation contract stay
the same. All guest index and replacement callbacks retain the active
execution token.

Both execution modes match all 21 isolated CPython 3.13.2 probes.
The reference matched four in each mode. Windows and Ubuntu each
pass 796 focused .NET 11 Release checks, including actual
suspension, awaited file close, terminal refusal and shared-budget
reuse. The first regression fixture used statically invalid
arguments and received frontend diagnostics. The final fixture
supplies those values through runtime globals. The original build and
fixture failures are archived.

## Focused comparison

The reference is `6466fbb52`. The two exact 32-file images use the
same Utf8Regex 0.4.0 adapter. The quiet Azure F4as_v7 ran Ubuntu 24.04.4
and .NET 11 RC3 on November 24, 2026 UTC. Two workers per image warm
for five seconds and provide seven rotated batches. All complete
goldens and 84 timed batches verify. Ranges span four pairings of
worker medians. Warm elapsed includes mediated RAM I/O, output checks
and previous-result disposal. Wider CPU also includes framing and
fixture reset. Compilation and startup are outside these clocks.

| Complete script | Warm elapsed change | Wider CPU change | Worker spread reference / candidate |
| --- | ---: | ---: | ---: |
| Module log scan | −3.52 to +1.84% | −4.76 to +1.64% | 2.92 / 2.56% |
| Compiled log scan | +0.71 to +1.95% | +1.52 to +5.12% | 0.62 / 0.62% |
| Quoted CSV invoice | −2.40 to +0.72% | −5.53 to +2.27% | 1.70 / 1.48% |

The compiled scan shows a small elapsed cost and higher CPU.
Module and invoice timings cross zero. Admitted peaks match.
Allocation matches for the compiled scan and varies by 174 B for
the module scan and 672 B for the invoice. Retain the shared
operation path for its Python ordering and reduced implementation.
Keep the compiled-scan cost visible at the next adapter checkpoint.

Both quiet gates, worker joins, fixture removal and durable archive
verification passed. The measurements compare Lython revisions with
the pinned adapter.

[Images](identity.json) · [Timings](quiet-summary.json) ·
[Allocation](allocation-summary.json) · [Before probes](reference-summary.json) ·
[After probes](candidate-summary.json) · [Procedures](procedures.tar.xz) ·
[Observations](quiet-observations.tar.xz) · [Terminal receipt](quiet-terminal-status.json).
