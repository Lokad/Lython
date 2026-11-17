# Representative JSON scripts versus CPython

R491/B908,2026-11-17UTC. Committed runtime557381fe/frozend739132f versus stock
CPython3.13.16; normal optional adapter0.4.0. Actual five-case frozen image and
goldens verified versus CPython3.13.2 before staging. Full changed-source
Windows/Ubuntu11/10/stress/installed-package delivery44116196658 is pending.

| Complete script | Compile ratio | Warm ratio | Compile-and-run ratio |
| --- | ---: | ---: | ---: |
| JSON-lines helper aggregation,4096records/110592B | 0.98–1.02× | 0.82–0.85× | 0.86–0.87× |
| Floating numerical JSON report,4096pairs/56495B | 0.90–0.92× | 1.99–2.03× | 1.67–1.76× |

Ratio=Lython elapsed/CPython elapsed; above one means slower. These are process
median combinations, not confidence intervals. JSON warm controlsL/C2.09/0.87%,
IQR≤0.49%; floatingwarm0.96/0.91%,IQR≤1.73%. Floatingfresh controls2.92/2.50% and
compileIQR≤5.15% limit precision. Warm and fresh medians are independently warmed
and not additive. No cross-collection causal source-change gain is claimed.

Quiet AzureF4as_v7/Ubuntu24.04.4,four EPYC9V45vCPUs/16GiB/no swap,.NET11RC3,
normal tiering/workstationGC; CPythonGIL/GC enabled. Six jobs204.45s,5second
rotated warmups/five approximately0.5second samples/two processes per engine.
Warm includes synchronousRun, mediated RAM input/output, full stdout/file goldens
and prior-result cleanup. Fresh adds compilation/compiled-owner disposal; compile
includes compiled-owner disposal. Startup, fixture reset/protocol and final cache
teardown are excluded; wider processCPU includes request work outside guest clocks.
CPython uses native buffered files without equivalent capability/budget checks.

The numerical warm gap remains clear with close controls; compilation does not
explain it. JSON-lines is faster in this named comparison. Neither generalizes to
all scripts, today’s unmeasured HEAD, startup, physical heap/RSS or embedding tails.

All correctness/quiet/Closed/exit/join/archive gates pass. RAM verified persistently
before unmount; terminal cached. Retired only archived3866core aliases and original
backed raw/archive replicas after fresh lease/path/UID/hash/link/all-alias proof;
qualifieddfc/currentd739/toolchains/canonicals and254/252MiB floors preserved.
Publication held; numerical attribution and substantial runtime roadmap continue.

[Exact timing/control data](summary.json),[raw](observations.tgz),[catalog](catalog.json),
[identity](identity.json),[custody](archive-receipt.json),[procedures](procedures.tar.xz),
[actual runtime image](../2026-11-17-shared-allowance-lock/main-frozen-identity.json).
