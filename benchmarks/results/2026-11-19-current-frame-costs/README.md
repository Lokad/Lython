# B1033 function construction and forwarding attribution

Profiled retained runtime `e93356adc` with core `b21ca2e4` on the quiet
Ubuntu Azure F4as_v7 and .NET 11 RC3 on 2026-11-19 UTC. These are the
exact two rule cases and expanded forwarding from the B1030 catalog.
The complete 32-file worker image verified with CPython goldens before
staging. Each case warmed for eight guest seconds. CPU stacks cover
roughly five seconds of whole-batch work per case.

| Case | Runtime stacks | Definition execution | Frame creation | Borrowed invocation | Governor admission/resize |
| --- | ---: | ---: | ---: | ---: | ---: |
| Rules | 1,079 | 325 | 12 | 289 | 127 |
| Annotated | 1,078 | 371 | 0 | 258 | 110 |
| Forwarding | 963 | 0 | 0 | 196 | 72 |
Among stacks whose nearest runtime frame is reserve, the defaults owner
accounts for 27 unannotated and 20 annotated samples. Shared record
allocation accounts for 20 and 31. The function constructor accounts for
18 and 19. Known default, annotation and function backing still request
separate admission for each definition. A combined existing loan can
target those repeated requests and preserve early approval across actual
guest callbacks. Closure retention and mutable namespace handling remain
separate.

Forwarding has 108 stacks with formatting ancestry, 57 with split
construction and 56 with rstrip. Copying expanded keywords is the nearest
runtime method on 36 stacks. This supports further inspection of the
formatting and input preparation boundaries. The counts overlap and do
not estimate removable CPU. They provide little evidence of a
frame-allocation bottleneck in these scripts.

Both quiet gates and output checks passed. Workers joined and RAM traces
were archived and verified under the lease before fixture removal.
Incomplete and ambiguous symbols remain in the raw evidence.

[Attribution](attribution.json) · [Raw observations](observations.tgz) · [Verified procedures](procedures.tar.xz).
