# Complete-script allocation origins

2026-11-13. Qualified976f9439/frozen97bb on local Windows/.NET11RC3. The full image
hashes are preserved, independently of the VM's earlier CPU images. Two existing
complete scripts receive four guest seconds warmup, then256captured-text or1024search
invocations. Goldens, actual worker/collector joins and fixture cleanup pass. This
is sampled allocation attribution; it is not a CPU/timing comparison, exact type
allocation total or retained-memory measurement. Event loss is not inferred absent.

| Script | Sampled triggers | Resolved production stacks | Leading triggering types |
| --- | ---: | ---: | --- |
| Captured text |2016|1989|PyString703; prepared-word cursor490; BigInteger377; temporary reservation244 |
| Search |1408|1286|PyString639; byte arrays259; BigInteger256; output builder120 |

The type names identify allocations triggering a periodic sample. They do not own
all bytes since the prior sample and cannot be called exact allocated-byte shares.
Other/unresolved ticks remain visible. Captures cover the complete worker request;
production-stack selection excludes ordinary fixture/protocol-only allocations,
while context construction, host I/O, result cleanup and executed helpers remain.

Per-line prepared-word cursors and reservation wrappers are a concrete text storage
opportunity. Current range arrays already use an execution-owned single-idle cache;
consider consolidating buffer ownership and private cursor state into that same loan,
removing the per-line cursor packet/transaction wrapper rather than adding a second
pool. Search allocation does not support a new call arena: line strings, byte input
and exact counter updates dominate its observed triggers. Existing frame optimizations
stay intact; these observations do not resolve the search's unknown CPU fraction.

The first dotnet-trace CLI attempt had no redirected readiness marker and produced
no usable capture; its command/log/partial trace remain archived. The corrected
collector reuses the managed diagnostics API, explicitly signals Attached, writes
the event stream and waits for method metadata rundown. Keywords0x8003 match the
installed tool's gc-verbose profile. It observes allocations/GC events and method
metadata; it does not request a collection, heap dump or guest/root walk. No profiler
or collector code/dependency enters production. Offline TraceLog resolves stacks.
The corrected collector's last assertion found six empty MSBuild/NuGet temp folders,
after both users/traces had joined. Their original inspection and checked empty-only
cleanup are preserved; this is a cleanup-check correction, not a repeated capture.
