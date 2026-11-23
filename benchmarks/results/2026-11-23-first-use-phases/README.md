# First use inside the comparison worker

A Windows diagnostic partitions the cold configuration edit observed
at B1451. The core is the exact frozen `dfabddb88` image. Only the worker
DLL changes to record phase timestamps. Seven executions per worker
version and workload alternate version order and rotate the cases.
Every process validates stdout and output files. The initial parser
expected an optional file digest for empty input. The corrected run
accepts the absent field for file-free controls and checks the digest
for the configuration edit. [B1451](../2026-11-23-runtime-cpython-checkpoint/README.md).

| Phase in the timed worker | Empty | Tiny arithmetic | Configuration edit |
| --- | ---: | ---: | ---: |
| Read and validate payload manifest | 6.1 ms | 6.3 ms | 14.1 ms |
| First public compilation | 25.8 ms | 51.9 ms | 143.5 ms |
| First public execution | 27.4 ms | 33.1 ms | 77.4 ms |
| Encode and write worker response | 34.3 ms | 28.2 ms | 27.9 ms |

These are process-local phase medians on the development machine. The
parent timings vary substantially across launches, including the
unchanged workers. The local direct command and traced variant have
different launch boundaries from B1451. Use the quiet VM for end-to-end
elapsed and this run to select work for attribution.

One local EventPipe sample of the same instrumented configuration edit
places 83.9 ms of inclusive stack time in the abstract interpreter,
within 122.1 ms in the frontend and 147.5 ms in compile core. Stack
intervals include native and unattributed time. The trace observes
`StaticContracts.CreateCallableContracts` initializing inside abstract
interpretation. It cannot isolate registry setup from other static
analysis work. The source combines independent immutable registries
in one static type initializer and builds a full callable table from
temporary and retained catalogs. The explicit initializer also checks
all built-in module registrations, submodule mappings and callable
aliases, regardless of the current script. The existing module parity
suite can verify those invariants. The next source pair should separate
demanded metadata from unrelated catalog construction, remove duplicate
staging and preserve the same diagnostics and public behavior.

[Summary](summary.json) · [Exact observations and worker patch](observations.tar.xz).
