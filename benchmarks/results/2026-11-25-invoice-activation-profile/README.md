# B1723 Quoted invoice execution profiles

The source-equivalent `93eead293` worker ran the complete 4,096-record quoted
invoice on the Azure F4as_v7 on November 25, 2026 UTC. The VM ran Ubuntu
24.04.4 and .NET 11 RC3. The complete 32-file worker image was verified.
Each mode warmed for at least eight seconds, then Linux `perf` sampled
whole-process CPU at 199 Hz. Each capture completed 1,024 verified runs.
The captures include fixture reset, protocol framing, mediated RAM-backed I/O,
output verification and result cleanup. Both modes matched the independent
CPython golden before and after capture. The quiet-machine gates passed and
all workers joined before the archive was verified and fixtures removed.

| Mode | Capture | All samples | Lython runtime in stack | Unmapped leaf | Entry or operand movement | CSV parse or row construction | Native instruction dispatch |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Warm `Run` | 6.22 s | 1,266 | 1,232 | 108 | 68 | 132 | 380 |
| Warm `RunAsync` | 6.43 s | 1,311 | 1,274 | 106 | 58 | 102 | 382 |

The final three columns classify the nearest resolved runtime frame. A
single instrumented worker per mode and incomplete inlining visibility limit
attribution. Native dispatch includes ordinary guest work. Counts cannot
establish savings from deleting an instruction or compare hosting modes.

The checked instruction layout gives `price_line` twelve slots, including
seven locals, and a 32-instruction native prefix. It performs four field
reads and integer conversions, two receiver calls, and a tuple return.
The four-instruction `rounded_basis_points` body already returns directly
from operands. The profile supports testing a shared subscription dispatch
that reads the receiver once and skips the integer probe for maps and
protocol objects. The separate sequence probe is the nearest resolved frame
in 41 sync samples and 40 async samples. The effect of the redesign remains
to be measured. In particular, preserve the numeric sequence path and use
an unaffected whole-script control.

[Identity](identity.json) · [Instruction layout](instruction-layout.json) ·
[Attribution](attribution.json) · [Raw captures](observations.tar.xz) ·
[Terminal receipt](terminal-status.json) · [Procedures](procedures.tar.xz).
