# Blanket packet retention: failed qualification

CI44098253722 at00adcc8a failed on both platforms:25internal/2public cases each.
The broad ownership inference was wrong. Ordinary packets are caller-owned
scratch through completion; universally retaining them caused real bounded-cache
and caught-invalid-factory failures, plus cleanup/refund regressions. Bound wrappers
separately promise fresh retainable receiver-prefix arrays. Their genuine major
storage needs existing execution ownership; that does not transfer every packet.

Keep all existing failing regressions unchanged. Restore ordinary temporary lifetimes,
prepared-constructor behavior and existing minor packet headroom. Delete only the
new assertions enforcing universal retention/exact-small-admission architecture.
Preserve independent bound retention/major-copy preflight/pending-funding coverage.

Exact committed inputs, terminal status and completed-job logs are cached here.
Never requery this run. No .NET10/stress/package success is claimed. B904 timings
describe6c808cd8 only; they do not qualify the corrected lifetime distinction.
