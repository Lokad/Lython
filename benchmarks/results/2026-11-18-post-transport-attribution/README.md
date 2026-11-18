# B1011 forwarding attribution after direct kwargs transport

Profiled the exact B1010 runtime `2215ec040` / `d1b9f71b` on the quiet
Ubuntu Azure F4as_v7 on 2026-11-18 UTC. The complete measured worker image
verified against its 32-file manifest. Both B1005 scripts in the custom
catalog verified against the actual frozen worker and CPython before
staging. Each case warmed for eight seconds before five seconds of CPU
sampling over whole batches. Both quiet gates passed and all workers
joined before verified archive and fixture cleanup.

| Script | Resolved runtime samples | Expanded-call ancestry | Final kwargs copy/binding | Split or unpack | Formatting |
| --- | ---: | ---: | ---: | ---: | ---: |
| Expanded file forwarding | 1,061 | 489 | 44 | 58 | 126 |
| Mutable bound report | 987 | 361 | 89 | 0 | 0 |

Groups overlap and include callee work. Counts do not estimate savings.
The accumulator's invocation method was the closest resolved runtime
method in 28 and 47 samples, respectively. The current special case
leaves the sole trailing mapping in its operand slot and builds the final
kwargs directly. It still starts a general call accumulator and writes
each preceding positional value into a packet. This is a smaller,
source-supported removal target than combining split and unpack.

The next prototype will pass positional operands directly from the
existing stack into owned callee slots. It keeps general expansion for
custom mappings and other call shapes. It must preserve callback order,
noncallable errors, duplicate-key precedence, reentrant admission
checks and funding until the last async user finishes. Compare complete
forwarding and mutable bound reports and a positional control before
retaining it. Split construction remains a separate text optimization
candidate. No timing comparison was made under profiling.

[Attribution](attribution.json) · [Raw traces and executed scripts](observations.tgz) ·
[Procedure and classifier](procedures.tar.xz) · [Terminal state](terminal-status.json)
