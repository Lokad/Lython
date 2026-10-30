# Owned whole-file input allocation diagnostic

Measured 2026-10-30 in Release .NET 11 RC3 on Windows x64 / Intel Core i7-14700KF.
Control is `e42eb376`; candidate `f3ce7ac3`. [Exact identities and samples](measurements.json)
accompany the [driver](Files.cs).

| Native acquisition of 64 KiB | Older returned-buffer path | First local migration prototype | Final owned-file path |
| --- | ---: | ---: | ---: |
| Binary, allocated bytes / 100 calls | 8,199,200 | 32,795,392 | 19,707,448 |
| UTF-8 text, allocated bytes / 100 calls | 1,646,400 | 41,083,640 | 19,723,080 |

The final path removes growth solely to discover EOF and removes the helper-only
streaming decoder/whole-result accumulation. It still allocates more than the old
boundary: admitted windows grow into a private complete input and fund a final
copy. The control text host lends its complete existing array, whereas the new
host fills caller storage. These are different acquisition/ownership contracts;
the table establishes no elapsed speedup, CPython ratio or complete Path.read_bytes
result. Path.read_bytes separately removes an additional result copy that this
native-acquisition diagnostic does not measure.

All calls validate the complete contents and return the root allowance to zero
after explicit owner cleanup, without GC. The sample includes native context
setup and excludes frontend, instruction dispatch, public projection, disk I/O
and asynchronous suspension. No timing, confidence interval or current-release
qualification is inferred. The intermediate row records an uncommitted prototype,
not an earlier shipped Lython revision. General streaming text-window conversion
is separate follow-on work.

To reproduce, compile the frozen driver as an optimized net11.0 console named
Lokad.Lython.WhiteBox.Tests against each recorded core DLL and its unchanged
Lokad.Parsing dependency, disabling default project globs. Define OWNED_FILES for
both builds, rebuild separately, and verify the emitted hashes. Legacy fixture
members exist only to reproduce the old control ABI; they are absent from the
current core contract and are not a runtime fallback.
