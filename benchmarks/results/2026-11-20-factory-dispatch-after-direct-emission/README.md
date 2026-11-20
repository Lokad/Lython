# B1193 Factory costs after direct statement emission

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-20
UTC. The frozen `2131039a4` core matches the runtime and worker sources
of `2dad15d55`. The complete 32-file image and both CPython goldens
verified before staging. Each worker warmed for at least eight seconds and
verified output before and after five seconds of CPU sampling.

| Stacks | Eager rules | Nested factories |
| --- | ---: | ---: |
| Identifiable runtime ancestry | 1,000 | 988 |
| Nearest runtime method is the instruction loop | 218 | 173 |
| Nearest runtime method is result completion | 77 | 70 |
| Nearest runtime method is memory reservation | 86 | 98 |
| Positional default capacity calls that reservation | 33 | 25 |
| Callable construction calls that reservation | 24 | 34 |
| Prepared keyword values call that reservation | 22 | 9 |
| Executable operand storage calls that reservation | 2 | 23 |
| General binder in ancestry | 21 | 16 |

The captures include warm execution, golden checks and result cleanup.
The first runtime method may sit below unrecognized or inlined work.
Inclusive ancestry counts can overlap. There are 67 and 57 unmapped leaves
in the two captures. The profile finds work across instruction execution,
result publication and several separately funded storage lifetimes. Generic
binding remains a small part of the sampled stacks. A new binder alone has
little support from this evidence. The prior grouped admission and
result-publication comparisons remain applicable to their measured designs.
This profile contains no before-and-after timing.

Both quiet gates passed. Workers joined and the raw observations were
verified in persistent storage under the VM lease before fixture teardown.

[Attribution](attribution.json) · [Raw observations](observations.tgz) ·
[Procedures](procedures.tar.xz) ·
[Measured core](../2026-11-20-direct-statement-emission/measured-core.dll.gz).
