# Current native function entry and transfer counts

An instrumented build of the retained `9fb7fdcfb` runtime runs the
seven B1437 scripts on Windows .NET 11 RC3. The ordinary and asynchronous
host entry points give the same counts. Both checks verify output, result
files and return of the host allowance.

| Complete script | Direct integer returns | Positional copies | Argument slots copied | General binding inside native entry | Native prefixes completed |
| --- | ---: | ---: | ---: | ---: | ---: |
| Postponed nested factories | 2,048 | 4,096 | 4,096 | 0 | 4,096 |
| Eager nested factories | 2,048 | 4,096 | 4,096 | 0 | 4,096 |
| Factories observing defaults | 0 | 4,096 | 4,096 | 2,048 | 6,144 |
| Fully supplied helper | 2,048 | 0 | 0 | 0 | 0 |
| Quote-heavy CSV invoice | 0 | 12,288 | 28,672 | 0 | 12,288 |
| Large positional control | 16,384 | 0 | 0 | 0 | 0 |
| Ordinary split report | 0 | 3,072 | 3,072 | 0 | 3,072 |

No native prefix transfers on these fixtures. Continuation construction
and copy work therefore cannot explain their measured gaps. Both supplied
arithmetic controls already use the direct return path. The factories'
remaining 4,096 positional copies each serve function construction. On
the observer, 2,048 further native starts bind the exposed defaults.
The invoice exercises the largest remaining positional transport route.
The earlier caller-tail trial reached the report alone on its fixture
set. A replacement should remove staging on the factory or invoice paths
and preserve the existing transfer and cleanup for genuine suspension.

The probe adds private instrumentation only to the isolated core.
The other 31 frozen files match B1437. All 14 invocations verify and
matched-script peaks agree between the host entry points. Counts describe
route frequency. CPU and elapsed evidence remain in B1437.

[Counts](routes.json) · [Image identity](identity.json) ·
[Instrumentation and procedure](procedures.tar.xz) ·
[B1437 comparison](../2026-11-23-single-owner-function-preflight/README.md) ·
[Earlier caller-tail trial](../2026-11-22-native-operand-call-loan/README.md).
