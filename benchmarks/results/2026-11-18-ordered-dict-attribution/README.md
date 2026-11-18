# Ordered dictionary traversal source and sampling gate

R544 sampled exact d9234f876 / 33025cee on Windows on 2026-11-18. Each
verified worker used ordinary GC and tiering, three seconds of warmup and six
seconds of managed thread sampling. The scaled manifest has 256 declared
targets and 192 emitted targets. The small manifest has four declared
targets. The invoice fixture contains 4,096 data records. Its nominal catalog size is 1,024.

| Complete script | Managed samples | Resolved Lython stacks | Native entry/version iterator samples |
| --- | ---: | ---: | ---: |
| Scaled manifest | 943 | 935 | 3 |
| Small manifest | 1,788 | 1,772 | 0 |
| CSV invoice | 1,875 | 1,862 | 0 |

The entry and version frames occupy the same three sampled stacks in the
scaled manifest. Counts overlap and describe local managed thread stacks.
They exclude external samples and include worker reset, I/O, validation and
protocol. They are insufficient as estimates of removable CPU. The earlier
allocation samples still show repeated enumerators, but these traces do
not support a native dictionary cursor redesign as the next performance task.

The invoice has 716 nearest samples at StartNativeCall and 380 at
FromIntegerSlots. Of the latter, 312 reach the module driver from the
instruction prefix and 68 include an initialized frame in their ancestry.
These are method locations and call paths, not a per-opcode or per-stage
CPU breakdown. The actual compiler gives price_line four-result unpack
intent, with its native MakeTuple immediately followed by Return. The
invoice module also has a four-item MakeTuple consumed by StoreSubscript.
The latter is an observable accumulator tuple. These facts argue against
treating all 380 samples as the old helper return round trip.

The first analyzer grouped shared byte builders with JSON byte output.
Refined attribution splits those groups using the original samples. The
first compiler-inspection build used properties already removed from the
current implementation. The corrected inspector reads current call and
tuple facts without rebuilding the measured worker. Both conditions remain
in the archive. Production source and benchmark ledgers stay separate.

[Sample counts and paths](attribution.json) · [Raw traces, compiler facts and verification](traces-and-verification.tar.xz)
