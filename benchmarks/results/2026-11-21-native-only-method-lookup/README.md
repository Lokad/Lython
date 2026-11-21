# B1297 native-only ordinary instance lookup

This refinement leaves the framed dispatcher unchanged. Its native
prefix uses the existing lookup for unshadowed instance functions
with Python's default attribute hook. It puts the evaluated receiver
and selected function into existing slots before evaluating arguments.
Native callers can stay in those slots when the callee completes.
Dynamic lookup and bodies that genuinely suspend transfer their live
values at the current instruction to the existing continuation.

Compared `acb41ad00` with `8f210d967` on the quiet Azure F4as_v7,
Ubuntu 24.04.4 and .NET 11 RC3, on November 21, 2026 UTC. Two processes
per image warm independently and provide seven rotated timed batches
each. Ranges span all pairings of process medians. Warm elapsed
includes execution, mediated RAM-backed I/O, golden verification and
prior-result disposal. Process CPU covers the wider request.
Compilation, startup and final teardown have separate clocks.

| Complete warm workload | Elapsed change | Wider CPU change | Local CLR allocation change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: | ---: |
| Nested method relay, 2,048 inputs | −31.51% to −27.59% | −30.71% to −26.81% | −71.72% | 2.88% / 2.77% |
| CSV invoice with nested policy methods, 4,096 data rows / 127,031 B | −9.57% to −8.94% | −10.68% to −10.13% | −29.92% | 0.14% / 0.56% |
| Direct and stored methods, 2,048 inputs | −4.88% to −1.90% | −5.77% to −1.58% | 0.00% | 2.22% / 0.90% |

Retain the native-only change. The method invoice and relay gains
survive the narrower implementation. The direct-method regression from
[B1296](../2026-11-21-native-method-lookup/confirmation/README.md)
does not recur. The direct control does not exercise the new lookup,
so its measured improvement has no attribution to it. Both eligible
workloads reduce accounted peaks and all five local workloads return
their allowance to baseline.

Windows .NET 11 and Ubuntu .NET 10 each pass 269 white-box and 1,558
public focused Release checks. These include selected-method identity
across suspension, terminal admission refusal, and host cancellation
and cleanup. The two complete 32-file images match five independent
CPython goldens. Both quiet gates and all 84 timed batches pass.
Workers join, guest fixtures are removed and the archive verifies
under the VM lease. The archive preserves the earlier direct-method
regression separately.

[Timing summary](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Source, checks and procedures](checks-and-procedures.tar.xz).
