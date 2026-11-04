# A9 current whole-text storage source gate (B204)

Current committedae9b82b7 source/frozen67373996 after list consolidation. Full
delivery41793317194 running separately. Text elapsed debt preserved. Earlier
B170/B191 profiles are not current CPU shares. One existing checked whole-text
worker, normal GC/tiering, four-second warmup,256 jobs with real file I/O/checks,
existing EventPipe allocation collector with readiness/stop handshake, no perf-map
or native JIT dump. Approximate allocation origins only, not timing or live memory.
Original quiet gates/lease, normal worker/collector joins, persistent evidence/RAM
fixtures;90s owned service bound. VM has33MiB free, so convert trace offline locally.

Source proof: whitespace splitting still allocates one UTF8 array and PyString
per word. PyString's separate committed-cache counter only describes one optional
rune-index array. Consider standard managed ArraySegment/ReadOnlyMemory backing
and deriving cache charge from that actual array, avoiding a metadata-size increase.
Eliminate per-fragment arrays using one admitted compact payload with ordinary
string windows. Payload stays owned by the existing execution batch; child
unpublished refunds must release only their own metadata/caches. No tracing,
identity ledger, new backing-owner infrastructure or borrowed-parent refund.
Current direct charge consumers inspected: fresh warning string, private decoded
read windows and freshly constructed cache keyword labels. General string
operations still need offset/Unicode/hash/identity semantics and cancellation.

End this gate in one concrete proposal/cost/ownership/deletion list or precise
deferral. Do not implement another scanner or infer CPU gains from sampled bytes.
If selected later, compare complete text BOTH APIs plus normal-string JSON/report
controls, preserving accepted B203 text debt and source identity; no external refresh.
