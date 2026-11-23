# B1434 Reuse the selected signature through native binding

Private `8ac282495` adds to B1433's signature consolidation. Native
call selection supplies the layout already read for an incomplete call.
Direct arithmetic and ordinary binding share it. A selected call with
complete positional supply takes the direct argument path. Binding reads
live defaults at lookup and the callable owns managed stack admission.

The reference is `47cdc8c80` with the exact B1431 core. Only the
core differs across the two verified 32-file images. The quiet
Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 23,
2026 UTC. The first worker order reverses B1433. An extra compile
lane examines the earlier time-to-result loss.

| Complete script | Boundary | Elapsed change | Wider process CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Postponed nested factories | Warm | +2.24 to +5.16% | +1.72 to +5.03% | 1.14 / 1.70% |
| Postponed nested factories | Compile to result | −4.34 to +0.62% | −3.61 to +0.65% | 0.40 / 4.76% |
| Postponed nested factories | Compile | −0.32 to +0.20% | −1.39 to +0.27% | 0.51 / 0.01% |
| Eager nested factories | Warm | −3.66 to +0.08% | −4.55 to −0.43% | 2.09 / 1.76% |
| Factories reading defaults metadata | Warm | −3.75 to +2.76% | −2.36 to +3.81% | 5.32 / 1.37% |
| Fully supplied helper | Warm | −0.04 to +1.92% | +0.48 to +2.44% | 0.74 / 1.22% |
| Quote-heavy CSV invoice | Warm | −2.57 to +1.13% | −1.81 to +0.70% | 2.69 / 1.08% |
| Large positional call control | Warm | −3.69 to −0.63% | −3.50 to −0.25% | 2.48 / 0.68% |

Ranges span pairings of medians from two independent workers per source.
Each worker warms for eight seconds and supplies seven rotated batches.
Warm elapsed includes applicable mediated RAM-backed I/O, verification
and previous-result disposal. Compilation includes compiled-owner disposal.
Time to result includes compilation, execution and both owners' cleanup.
Wider CPU includes resets and protocol. Startup and final teardown are
outside elapsed.

Local allocation decreases by 32,776 bytes for each factory script.
That is 3.72% for both ordinary factories and 2.67% for the observer.
Both ordinary controls and the quoted invoice change by at most 10 bytes.
Admitted peaks match and every allowance returns to baseline.

Keep both designs private. The refinement retains the architectural
and allocation gains. It reduces eager CPU and wide positional elapsed
and CPU. The earlier postponed first-result loss does not recur, with
4.76% candidate spread. Compile-only elapsed spans zero with tight
workers. Postponed warm slows in every pairing, and so does fully
supplied process CPU. The observer has 5.32% reference spread.
The measurement leaves the earlier first-result cause unresolved.
Local tier-disabled JIT output grows by another 14 bytes in native
call entry, while the binder retains its smaller size. The JIT output
describes generated code shape. The external comparison checks semantics.

Windows .NET 11 and Ubuntu .NET 10 each pass 2,699 focused Release
checks. Both images match six CPython goldens, 21 executable tape
fixtures, and the mixed binding and reentry probe from B1433.
All 224 timed batches and both quiet gates pass in 438 seconds.
Workers join, guest fixtures are removed and the persistent archive
verifies under the lease. The only retired replica is the byte-proven
B1433 working core.

The separate text diagnostic on the reference changes one complete
report from immediate iteration to a local assignment of its word list.
The tapes confirm the respective `Call GetIter` and `Call StoreLocal`
sequences. Both variants match CPython and the same report bytes. Local
allocation rises from 380,464 to 978,136 bytes and admitted peak from
1,278,972 to 1,971,056. The diagnostic measures the complete route with
the list and its ownership included. The procedures include the new
fixture, tapes, allocation and decoded input inventory.

[Source and images](source.json) · [Timings](summary.json) ·
[Allocation and admission](local-allocation.json) · [Observations](observations.tgz) ·
[Procedures](procedures.tar.xz) · [Archive receipt](archive-receipt.json) ·
[B1433 first trial](../2026-11-23-body-owned-signatures/README.md) ·
[Reference core](../2026-11-23-direct-bound-integer-refined/README.md).
