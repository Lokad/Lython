# B1296 direct-method control for native instance lookup

The [B1295 prototype](../README.md) removes caller-context creation
for nested ordinary method lookups. This control calls the same method
through an attribute and through a stored bound method at module
scope. It has no nested Python caller to keep inline.

The same `acb41ad00` and `e8a74e81f` 32-file images ran on the quiet
Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. Two independently
warmed processes per image contribute seven rotated batches each.
All pairings show a regression.

| Complete warm workload | Elapsed change | Wider CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Direct and stored methods, 2,048 inputs | +6.07% to +9.18% | +4.98% to +9.72% | 1.00% / 1.91% |

Elapsed includes execution, golden observation and prior-result
cleanup. The script has no guest filesystem I/O. Wider process CPU
includes the worker request. Compilation, startup and final teardown
have separate clocks. Both machine gates and all 28 timed batches pass.
Workers joined, fixtures were removed and the archive verifies under
the lease. Both full images matched the single-case catalog before staging.

Keep the shared-lookup candidate private. The pace of the direct-method
control confirms its adverse local result. A refinement keeps the
ordinary framed member load intact and extends only the native helper
prefix through the existing Python instance lookup. The first
experiment's substantial relay and invoice gains remain the target.

[Timing summary](summary.json) · [Raw observations](observations.tgz) ·
[Image verification and procedures](procedures.tar.xz).
