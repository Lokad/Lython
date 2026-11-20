# Postponed annotation text attachment

`4deab5e86` prepares canonical postponed annotation text during lexical
preparation. It deletes a complete suite traversal and a second future-import
query. Syntax validation retains its existing order. The writer still uses
the original source spans, compilation owner and active token.

The Windows .NET 11 allocation comparison uses `c1c15c008` and
`4deab5e86`. The reference core is the exact `8643ebfd8` B1172 image,
whose source matches `c1c15c008`. The compile diagnostic disables tiered
compilation for both roles and includes code-owner disposal.

| Script | Bytes per compile before | Bytes after | Change |
| --- | ---: | ---: | ---: |
| Postponed rules | 64,160 | 63,784 | −0.59% |
| Postponed nested factories | 76,440 | 76,064 | −0.49% |
| Postponed rules with annotation inspection | 71,232 | 70,848 | −0.54% |
| Eager rules | 57,392 | 57,304 | −0.15% |
| Positional invoice | 168,616 | 168,528 | −0.05% |
| Generated validator | 3,048,494 | 3,048,406 | Flat |

Both complete images matched six CPython goldens. Warm allocation and
accounted peaks matched, with allowance returned after cleanup.
The integrated source passed 385 focused Release checks per platform on
Windows .NET 11 and Ubuntu .NET 10, including compiler cancellation and
shared-budget refusal. This is a traversal deletion with a small
allocation benefit. Elapsed time and CPU remain unmeasured.

[Summary](summary.json) · [Source and diagnostics](diagnostics.tar.xz) ·
[Reference core](../2026-11-20-utf8-format-specifiers/measured-core.dll.gz).
