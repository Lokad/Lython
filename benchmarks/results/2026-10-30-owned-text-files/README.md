# Owned text-file allocation diagnostic

Measured 2026-10-30 in Release .NET 11 RC3 on Windows x64 / Intel Core i7-14700KF.
Control is `acc0a8b9`, candidate `e42eb376`. [Exact assembly identities and raw
counts](measurements.json) accompany the [driver](Files.cs).

| Native workload | Managed bytes / 100 iterations before | After |
| --- | ---: | ---: |
| Binary: read 64 KiB in 4 KiB chunks | 16,780,248 | 16,801,848 |
| Binary: stage, publish and close 64 KiB | 14,390,112 | 14,392,576 |
| UTF-8 text: read 64 KiB in 4 KiB chunks | 38,215,008 | 31,471,704 |
| UTF-8 text: stage, publish and close 64 KiB | 21,396,248 | 14,764,520 |

Text handles remove copied host range payloads and unchanged UTF-8 publication
copies. Binary handles acquire a shared transport object; their small allocation
increases remain visible. Text window decoding still has a UTF-16 conversion,
which this migration does not remove. All cases use the same ASCII input and
verify every byte, complete publication and zero root balance after each owner
ends, without GC. Host input is pre-existing storage; writes consume their borrow
without retaining a second output copy.

The diagnostic warms twenty invocations and samples one hundred with the
current-thread allocation counter. It includes native execution context creation,
guest storage, file operations and ownership cleanup. It excludes compilation,
instruction dispatch, public projection, disk I/O and asynchronous suspension.
These observations establish no elapsed speedup or CPython ratio and do not
update the primary comparison table or qualify whole-file/path helpers.

Reproduce as a Release net11.0 console named Lokad.Lython.WhiteBox.Tests, with
the recorded core DLL and unchanged Lokad.Parsing dependency. Disable default
project globs, rebuild each side separately and verify emitted core hashes.
Define OWNED_FILES on both sides: binary already uses the owned contract in
the control, whereas text uses its legacy transport. Both hosts implement the
actual transport requested; no compatibility fallback is added to either runtime.
This frozen driver targets those recorded internal APIs, not future HEAD.
