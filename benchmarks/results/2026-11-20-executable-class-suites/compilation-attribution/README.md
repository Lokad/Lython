# B1194 class compilation allocation attribution

A local Windows .NET 11 EventPipe trace compiled the same generated dataclass
workload on the two existing 32-file frozen images. Each image was verified
before and after profiling. The workload defines 49 classes, mostly sibling
classes with one annotated assignment. Every invocation disposed its code
owner and returned the allowance to baseline.

The reference had 18,309 allocation ticks with a resolved compiler root,
and the candidate had 21,550. In the candidate, the reported origins below
class definition emission include builder construction, name interning,
instruction buffers, control-flow setup and final instruction layout.
Within the reported class-definition origins, sampled allocation triggers
were most often attributed to constructors and the body compiler. Actual
allocation per compile comes from the separate current-thread diagnostic.
The allocation ticks identify origins and do not measure CPU, retained
bytes, or an exact allocation breakdown. The reader keeps the top twelve
origins per allocation type, and inlining can obscure ancestry.

The private refinement `90de89dde` removes unnecessary graph preparation
from single-block code and omits empty-region normalization. These paths
are shared across class and ordinary code. Dataclass compile allocation
falls from 770,496 to 750,376 bytes, still 17.7% above the 637,504-byte
reference. Local eager rules, manifest and invoice decrease by 920, 1,040
and 800 bytes respectively. Generated validator allocation is effectively
flat. Compiled-owner disposal returned the borrowed allowance to baseline.
The exact Release image passed 375 white-box and 830 public checks on
Windows .NET 11, including protected control flow, cancellation and refusal.
This refinement has no quiet elapsed or CPU measurement. Reducing the cost
of constructing or laying out each small suite, including buffers and maps,
remains the next step before a new whole-script comparison.

[Trigger summary](trigger-summary.txt) · [Trace hashes](identity.json) ·
[Traces and procedures](trace-and-procedures.tar.xz) ·
[Refinement allocation](refinement-allocations.json) ·
[Refinement checks and patch](refinement-checks.tar.xz) ·
[Whole-script observations](../README.md).
