# setdefault correctness and ordinary calls

The batch fixes observable Python behavior before removing call packaging:

- `6dc9fcee` applies existing positional-only signatures to dict/defaultdict
  `setdefault`. Seven old-path failures pass after; custom methods keep their
  declared keywords. Argument effects finish before rejection, without hashing
  the key or mutating the dictionary.
- `5bde22cd` shares the existing protocol insertion kernel with insert-only
  lookup. A guest query hashes once on misses and identity hits. Indexed protocol
  keys are not compared again in the native-key scan; callback-inserted values
  are read live. Existing insertion sequence metadata lets the operation examine
  new replacement keys without replaying old candidates. The two builtin
  lookup-plus-write bodies and replaced periodic-work/probe counters disappear.
- `bb2631a7` uses the existing receiver/operand path for ordinary one/two-positional
  calls on exact native dictionaries. General/exposed calls share the same
  operation. Dynamic, expanded, keyword and invalid-arity forms retain binding.
  No new dispatch framework, option or public C# API.

Storage and side-index preflights, rollback, first owners and aliases remain.
Probing snapshots stay admitted through callbacks. An ungoverned internal
defaultdict now attaches its existing wrapper/backing before lookup and can
refuse earlier; governed owners remain first. No independent allowance, tracing,
forced GC, memory ledger, quota or admission retry is introduced.

Validation preserves [seven keyword failures](keywords-red.txt), [three key
protocol failures](protocol-red.txt) and [four argument-packaging failures](direct-red.txt).
Focused Release passes include 96 public and 51 white-box storage/protocol controls,
then 74 public call/order/lifetime checks and 14 direct-call/preflight checks.
These selections overlap and are not a unique-test total. The isolated post-I/O
hash-recursion fixture unwinds and recovers three times at its unchanged 2 MiB
allowance. Cancellation during hash/equality, terminal refused admission, retained
default aliases and immediate shared-budget reuse pass. No timeout, scale, budget
or containment assertion was relaxed. [Receipts](checks.json), [18 independent
protocol goldens](oracles.json) and [six call goldens](call-oracles.json) are retained.
This does not add asynchronous hash/equality capabilities to `setdefault`.

The before worker was frozen **after both correctness fixes**, at `5bde22cd`.
The single short comparison measures only the later positional-call change:
2,048 calls cycling over 16 integer keys, then exact output `16 120`. Mutable
state is recreated per invocation. Both engines use identical source and golden.

| Engine | Warm synchronous complete kernel | IQR / median | Status |
| --- | ---: | ---: | --- |
| Lython `5bde22cd` | 0.472 ms | 0.90% | Before; diagnostic |
| Lython `bb2631a7` | 0.393 ms | 2.20% | After; diagnostic |
| CPython 3.13.2 | 0.102 ms | 0.39% | External baseline; diagnostic |

The candidate median is about 16.7% lower in this one capture. The observation is
unqualified; it establishes no general speedup, individual callback-fix gain,
allocation reduction or embedding latency. The after/reference ratio is about
3.84 (Lython elapsed / CPython elapsed). No retiming or full lane.

Measured 2026-10-20 UTC on shared LOKAD-0399, Windows build 26200, x64 with 28 logical
processors, .NET 10.0.13/workstation GC, ordinary CPython GIL/GC and an explicitly
unbounded timing root. Construction, execution/result cleanup and golden checking
are included; compilation, startup and transport are excluded. Seven alternating
rounds follow warmup. Parent and all workers finish in 7.290 seconds without forced
collection. [Declaration](declaration.json), [frozen identity](baseline.json),
[catalog](catalog.json), [samples/loaded binaries](micro.json) and [process receipt](micro.receipt.json)
preserve the boundary and evidence.

Full Windows/Ubuntu Release, original Stress and both explicit package consumers
pass at exact runtime revision `bb2631a7`: [CI 39345224537](https://github.com/Lokad/Lython/actions/runs/39345224537)
and [inspected final gate](delivery.json). Four spaced status queries; terminal
green is cached and must never be queried again. No publication.
