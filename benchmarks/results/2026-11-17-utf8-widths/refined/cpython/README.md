# Complete text normalization against CPython

B900,2026-11-17UTC. Committed **dbdf8262 / frozena95ff2a4**, stock **CPython3.13.16**.
Full runtime delivery44080812207 is pending independently. Original generator
text script,4096lines/52224B mixed Unicode input; reads, normalizes newlines/tabs/
trailing spaces, joins, writes and checks exact stdout/files.

| Phase | Lython | CPython | Lython / CPython | Measurement status |
| --- | ---: | ---: | ---: | --- |
| Compilation only | 0.035–0.036 ms | 0.037–0.037 ms | 0.94–0.96× | R483; diagnostic; controls L/C 2.06/0.15%; IQR≤1.09% |
| Warm execution | 0.913–0.915 ms | 0.505–0.522 ms | 1.75–1.81× | R483; diagnostic; controls L/C 0.25/3.40%; IQR≤0.75% |
| Compile and execute once | 0.983–0.987 ms | 0.590–0.592 ms | 1.66–1.67× | R483; diagnostic; controls L/C 0.45/0.36%; IQR≤0.60% |

Ratio above one means Lython is slower. Two independent processes per engine;
process-median combinations are not confidence intervals. Warm CPython control
movement3.40% limits precision; retain all raw controls. Fresh controls0.45/0.36%.
This external baseline is not causal source gain across collections or attribution
of the remaining gap. Earlier unfavorable results remain preserved.

Three phases103.55seconds, quiet AzureF4as_v7/Ubuntu24.04.4,4EPYC9V45vCPUs,
16GiB/no swap; .NET11RC3 natural workstationGC/normaltiering,CPythonGIL/GC.
Rotated5second full-request warmups/five approximately0.5second samples. Warm
includes synchronous execution, mediated RAM I/O, exact stdout/file validation
and prior-result cleanup. Compile includes compiled-owner disposal; fresh adds
compilation/compiled-owner disposal. Reset/staging/protocol/startup/final cache
teardown excluded. Independently warmed phases are not additive. WiderCPU also
includes reset/protocol. CPython files have no equivalent host authorization or
shared-memory checks. No startup, genuine suspension, RSS or embedding-tail claim.

Same actual-worker-verified5case catalog/copied32file image as source comparison.
All identity/golden/quiet/Closed/exit/join/archive checks pass; RAM removed and
terminal cached. Only exact committed B899 raw replica retired with fresh path/
UID/hash/link/all-alias proof; current images/toolchains/canonicals and254/252MiB
floors preserved. Publication/tagging held; broader performance work continues.

[Summary](summary.json),[raw](observations.tgz),[identity](identity.json),
[custody](archive-receipt.json),[procedures](procedures-and-checks.tar.xz).
