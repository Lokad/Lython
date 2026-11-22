# Native function routes after B1415

An instrumented copy of the `76b7934f7` core counts entry routes on the
six B1415 scripts. Windows .NET 11 RC3 runs each complete script through
both host APIs, preserving mediated file I/O, expected output and
allowance cleanup. The two host APIs give identical counts.

| Complete script | Framed native entries | Nested native entries | Framed fallback entries |
| --- | ---: | ---: | ---: |
| Postponed nested factories | 6,144 | 0 | 0 |
| Eager nested factories | 6,144 | 0 | 0 |
| Factory observing defaults | 6,144 | 0 | 0 |
| Fully supplied helper | 2,048 | 0 | 0 |
| Quote-heavy CSV invoice | 4,096 | 8,192 | 0 |
| Large positional call control | 16,384 | 0 | 0 |

Every framed native candidate enters that route. No script uses the
context-free deferred leaf. Repeated target classification in framed
fallback therefore cannot account for the measured factory, positional
or invoice gaps. Leave that fallback in place. The factory's three
framed native calls per iteration remain distinct from its three
construction admissions.

The diagnostic core differs from its matched worker image only by the
instrumented DLL. All 12 executions match the previously verified
goldens, including expected result files, and restore the allowance.
These route counts carry no CPU or elapsed estimate.

[B1415 comparison](../2026-11-22-native-call-selection-single-proof/README.md) ·
[Route counts](routes.json) · [Image identity](identity.json) ·
[Instrumentation and procedure](procedures.tar.xz).
