# B154 current config execution gate

B152 exact bd3 committed source: config fresh2.101ms/CPython0.334, warm1.974/0.241;
compile-only57.84us/34.68. Controls limit precision but compilation is insufficient
as explanation. Reuse one existing perf cpu-clock/fp capture,2048 complete config
jobs, same golden/I/O/cleanup boundary and actual guest thread. Profiling elapsed
is not a before/after performance verdict. Wait for B153 disposition to choose
actual frozen retained image; no concurrent VM capture and timing.

Source shows JsonEncodeIterator currently creates/releases a governed byte builder
on every next chunk, materializes an independent PyString, then writes through
already-buffered text-file storage. A reusable iterator-owned builder/fragment
transport is only a candidate. Preserve public chunk/alias semantics, custom writes,
callbacks, reentrancy, partial failures, cancellation, capacity overlap and owned
cleanup. Do not promise gain or implement a second serializer before attribution.
Stop after one usable capture/source map or document uncertainty; bounded corrected
observation is allowed if an actual capture defect prevents attribution. No broad
new measurement campaign or framework.
