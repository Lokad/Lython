# B1085 argument binding scan removal

The private `757eb1716` prototype skips the filled positional prefix and
passes the first unfinished keyword-default slot directly to the protocol
continuation. It shares the existing binding and rollback routines. The
deferred-default regression places the protocol lookup between two supplied
keywords and verifies the result after genuine host I/O suspension and
cleanup. No public contract changes.

| Workload | Guest elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Unannotated rule closures, warm | −1.40 to −0.82% | −2.97 to −2.91% | 0.23 / 0.36% |
| Annotated rule closures, warm | −4.22 to +1.01% | −3.19 to +1.77% | 1.50 / 3.89% |
| Reused defaults, warm Run | −3.52 to +1.30% | −3.29 to +2.19% | 1.58 / 3.36% |
| Reused defaults, warm RunAsync | −4.14 to −1.04% | −4.26 to −1.18% | 2.95 / 0.28% |
| Positional calls, warm | −1.28 to −0.08% | −0.41 to +1.58% | 0.13 / 1.10% |
| Expanded forwarding report, compile-to-result | +0.40 to +1.73% | +0.88 to +1.78% | 0.77 / 0.54% |

Keep the prototype private. Ordinary rules and the RunAsync defaults
workload improve in this pair. The precise attribution between the two
removed scans remains unresolved. Annotated and synchronous defaults span
zero, and complete forwarding is slower with lower same-source spread and
supporting CPU. Allocation and accounted peak are effectively unchanged.
Revisit the scan removal if it can retire another part of binding or avoid
the shared-path regression without duplicating the binder.

`a9510f049` and the candidate ran on the quiet Azure F4as_v7, Ubuntu 24.04.4
and .NET 11 RC3 on 2026-11-19 UTC. Both complete 32-file images matched five
CPython goldens before staging. Two fresh processes per source ran five
rotating timed batches per workload/phase. Changes span all four pairs of
process medians. This is a Lython source pair. The maximum single-process
elapsed IQR is 2.29% of its median.

Warm includes mediated RAM I/O, output goldens and previous-result cleanup.
Compile-to-result adds compilation and compiled-owner disposal. Startup,
fixture reset, request framing and final cache teardown are outside guest
elapsed. Process CPU has a wider boundary. All 120 timed batches verified
in 273.89 seconds. Workers joined, fixtures were removed and the persistent
archive verified under the lease.

Each local target, Windows .NET 11 and Ubuntu .NET 10, passed 272 focused
Release checks, then both targeted versions of the updated suspension
regression. The allocation check restored the shared budget baseline for
every workload on both images.

[Elapsed, CPU and allocation](summary.json) · [Raw observations](observations.tgz) ·
[Source, exact test logs and procedures](checks-and-procedures.tar.xz).
