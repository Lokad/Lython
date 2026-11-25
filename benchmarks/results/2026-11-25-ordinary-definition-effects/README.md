# B1728 Generated definitions with default effects preserved

The original factory builds 96 local functions on each call and returns
three. The diagnostic source replaces the 93 other declarations with
their 186 positional and keyword default expressions in evaluation order.
It preserves 156 dynamic calls to `shift` and leaves the returned functions
and metadata reads intact. Both complete scripts verify against independent
CPython output. This is a workload comparison on the same production
runtime. The diagnostic source gives no compiler proof for removing a
definition in a program that can observe its locals.

| Warm workload | Lython | CPython | Lython / CPython |
| --- | ---: | ---: | ---: |
| Complete generated definitions | 0.30487–0.30861 ms | 0.14933–0.15185 ms | 2.008–2.067× |
| Default effects with three returned functions | 0.18653–0.18751 ms | 0.06190–0.06469 ms | 2.883–3.029× |

The diagnostic takes 38.49–39.56% less warm elapsed on Lython and
56.68–59.23% less on CPython. A large gap remains through default effects
and the three returned calls. The changed guest tape affects dispatch as
well as storage. The difference cannot isolate the CPU cost of allocating
function shells. Profile the remaining default evaluation and argument
transport before choosing a construction redesign.

The verified 32-file Lython image uses production source `857cc8001` and
stock CPython 3.13.16, as in [B1727](../2026-11-25-formatted-report-cpython/README.md).
The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 25,
2026 UTC. Two workers per engine warm for at least five seconds and provide
seven rotated batches per workload. Rows span all combinations of worker
medians. Elapsed covers warm execution, output verification and result
cleanup. There are no guest file operations. Reset, framing, compilation
and startup sit outside the elapsed boundary.

Both quiet gates and all 56 timed batches pass. All workers join, guest
fixtures are removed and the persistent archive verifies under the lease.

[Identity](identity.json) · [Timing](summary.json) · [Within-engine changes](effects-summary.json) ·
[Observations](observations.tar.xz) · [Terminal receipt](terminal-status.json) ·
[Exact workload delta and procedures](procedures.tar.xz).
