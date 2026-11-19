# B1018 runtime attribution for rule closures and forwarding

Profiled `6c1da75b7` with frozen core `c9969271` on 2026-11-19 UTC, on the
quiet Ubuntu Azure F4as_v7 with .NET 11 RC3. The three B1016 scripts
verified against the exact 32-file image and golden outputs. Each case
warmed for eight seconds before a five-second whole-batch CPU capture.

| Workload | Resolved runtime samples | Prepared function definition | Borrowed invocation | Memory reserve as nearest runtime frame |
| --- | ---: | ---: | ---: | ---: |
| Expanded forwarding | 1,002 | 0 | 217 | 60 |
| Unannotated rule closures | 1,115 | 367 | 243 | 147 |
| Annotated rule closures | 1,104 | 389 | 259 | 142 |

Definition and invocation counts include ancestry and overlap. The
nearest reserve caller is pending default construction in 45 unannotated
and 37 annotated samples, and function creation in 23 and 51 respectively.
Annotated rules include 59 samples with metadata in their ancestry.
Forwarding has 113 with formatting, 54 with split construction and 42 with
file I/O in their runtime ancestry. These counts guide source inspection.
They do not estimate the gain from removing a named method.

The first run completed forwarding and unannotated rules. A cumulative
capture limit sized for two jobs stopped the third before its closing
verification. The failure also interrupted ownership normalization of the
third trace. Recovery verified the bytes, archived the complete partial
capture and unmounted the fixtures once all workers had exited. The
annotated row comes from a bounded single-case confirmation of the same
image and script. That run passed both quiet gates, output checks and cleanup.

The next redesign tests one completion path for ordinary executable
definitions, including prepared default storage and annotation publication.
The existing state machines should stay only where evaluation or a
decorator can suspend. Preserve Python ordering and metadata identity,
preflight before allocation, live ownership through callbacks, and terminal
refusal with awaited unwind. Compare both rule scripts against the same
reference with forwarding and positional controls.

[Initial attribution](attribution.json) · [Original raw capture](observations.tgz) ·
[Original terminal status and recovery receipt](terminal-status.json) ·
[Annotated confirmation](annotation-confirmation/attribution.json) ·
[Confirmation raw capture](annotation-confirmation/observations.tgz) ·
[Procedures](procedures.tar.xz)
