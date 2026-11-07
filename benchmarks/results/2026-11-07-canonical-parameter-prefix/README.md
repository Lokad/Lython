# Canonical compiler parameter prefix — R32

2026-11-07. Complete R31's parameter metadata design: executable local slots use
the same prepared named/variadic order as the binder. Remove Builder's raw parameter
list and separate two-pass classification. Keep function/module distinction and
body-local maps, directives, transitive closures and synthetic generator bindings.
Lowered definitions prepare their layout and optional generator together; there is
no second ordering algorithm, cache, dispatch route or ownership mechanism.

Sourcebf279768 plus the preserved seven-file frontend patch; exact checked core
net11 77bb12cc / net10 0f9553f9. Runtime implementation source is unchanged.
Full source patch, initial compiler initialization/name failure and corrected
commands/logs are retained with the exact checked images.

Release build zero warnings/errors; per actual.NET11/.NET10,406internal/1,962public
focused checks pass: prepared functions/defaults, class/super/scopes, annotations,
lambda/comprehension/generator behavior, cancellation and shared admission/lifetimes.
All four actual-target/API generator probes independently match CPython3.13.2.

One short existing complete-job diagnostic compares R31 33384376, current and an
identical-before image: separate compile/warm, four cases,32warmups and five rotated
16-job rounds. All stdout/file goldens, funding, isolation and fixture gates pass.
Compilation allocation falls128B for the four-definition class factory,32B for
text/CSV's helper. A synthetic generator expression adds152B (+0.385%) for its
explicit empty prepared function layout; retain this small compilation tradeoff.
Warm allocation is flat (observed differences below0.02%; generator exactly flat).
No quiet-machine timing was run for this compiler consolidation; desktop clocks
remain diagnostic. Do not transfer R31's measured percentages to this exact image.

Full Windows/Ubuntu, actual-target ordinary/original Stress and installed Release
consumers are batched once for the retained R30–R32 delivery. That qualification is
pending; prior R29 green receipts do not qualify this source. Text/CSV uncertainty
from R31 remains open at the next coherent execution milestone. This completes the
parameter-layout consolidation, not the broader complete-script performance goal.
