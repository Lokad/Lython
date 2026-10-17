# Direct uppercase call allocation diagnostic

One local Windows capture on 2026-10-17 UTC compares Lython45fb2892 with the
[frozen two-file call-transport patch](candidate-runtime.patch). It measures
managed allocation in a warm128-call native loop; compilation,invocation setup,
input construction,startup and host I/O are outside the boundary. It establishes
no elapsed-time,CPython multiplier,RSS or embedding-latency improvement. The
complete bulk-uppercase job makes one upper call;its benefit is unmeasured.

| Call form | Synchronous baseline → candidate | RunAsync baseline → candidate |
| --- | ---: | ---: |
| Immediate text.upper() | 48,640 →32,256 bytes | 60,904 →32,232 bytes |
| Exposed method control | 44,552 →44,552 bytes | 60,912 →60,912 bytes |

Each cell is the median of eight samples after four warmups. Every sample in
each cell is identical. The128 distinct inputs are row0ß through row127ß;
uppercase results use the existing UTF-8 casing/storage implementation. The
native async loop must complete inline. GC,tiering and protection settings are
ordinary;there is no forced collection or long benchmark lane.

The patch adds exact-str no-argument upper to the existing receiver/delegate
route,and shares the original governed upper body with exposed methods. Other
receivers and call forms retain ordinary binding. The subsequent finite Unicode
expansion-data correction is separate from this frozen measurement;these numbers
must not be presented as today's HEAD or as a measurement of that data change.

Machine:Intel Core i7-14700KF,28 logical processors,Windows10.0.26200 x64.
Release .NET10 test assembly;SDK10.0.300-preview.0.26177.108. Installed dotnet host
was10.0.13;the exact test-runtime patch was not captured with the original samples.
PowerShell's10.0.12 runtime is recorded separately and is not a test-runtime claim.

[Evidence](evidence.json) records all samples,source/binary identities,machine,
process receipts and bounds. Raw TRX files preserve the original output. The
[temporary probe](UpperAllocationProbe.cs.txt) was removed from the shipping test
suite. To reproduce:check out45fb2892,copy the probe to the white-box test folder,
build Release and select UpperAllocationProbe;apply the frozen patch and repeat
once. Remove the temporary source afterward. The probe uses the existing test
harness and no new benchmark framework. Keep ownership cleanup and the existing
28-second process limit plus one second for cleanup. This historical native
recipe is not part of ordinary qualification or a reason to rerun long lanes.
