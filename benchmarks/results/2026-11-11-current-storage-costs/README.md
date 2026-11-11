# Current runtime costs after dictionary storage — 2026-11-11

Two short profiles of retained `8f8628a4`, using its exact frozen refined R245
core, fixed `fec79` worker and optional adapter 0.4. Runtime source is unchanged
by `458be840`, which updates documentation and test scheduling. Actual code/maps
come from these processes; old offsets and distributions are not transferred.

| Complete script / API | Guest CPU seconds | Guest leaf samples | Unknown |
| --- | ---: | ---: | ---: |
| Captured text / Run | 5.78 | 1,148 | 71 |
| JSON-to-Markdown report / RunAsync | 5.15 | 1,020 | 60 |

Text leaves associate 139 with the native dictionary loop, 107 with word preparation,
82 with dictionary accumulation, 80 with the shared instruction loop, 63 with native
iteration, 60 with proof and 57 with native evaluation. Report associates 120 with
the instruction loop, 54 with input dispatch, 51 with writes, 47 with formatted value
emission, 39 with formatted instruction handling and 38 with JSON construction.
Inlining and useful work overlap these labels; they are not additive removable
fractions. All host/reset/protocol threads and unknowns remain in the raw record.
No elapsed comparison, allocation attribution or speed forecast is claimed.

The bounded source review prevents repeating completed work:

- Execution contexts already serve as their executable frames. Ordinary helpers
  can execute a borrowed prefix before promoting context; final argument/local
  storage already shares one backing. Call-related method names do not establish
  duplicate context/frame allocations.
- Actual saved Unix Tier1 code uses 72-byte native nodes and copies a 32-byte
  instruction during evaluation. The representation is smaller than an assumed
  wide instruction. Native preparation precedes operand forwarding/linearization;
  direct indexing of final code would require another mapping. No metadata rewrite
  is selected without evidence of dominant removable work.
- Text reading already borrows unchanged UTF-8 windows without UTF-16 decoding.
  Returned values copy because the raw window is mutable/reused. CSV already
  writes into its caller's destination. A new shared-view/batch scheme adds
  lifetime/admission invariants; no such layer is introduced.
- These text scripts call `lower().split()` and contain no redundant `strip()`.
  Inline words and existing casing/split integration remain. .NET 11 grouped
  integer formatting already uses UTF-8 `TryFormat`; a BCL formatting symbol
  does not establish an avoidable UTF-16 conversion.

The next source gate assesses native-loop stack admission: one scope for a native
segment could replace per-word entry/exit and its inner cleanup. It must preserve
first binding/effect order, empty loops, reentry, protocol/growth fallback, first
refusal, cancellation, exact promotion and validated managed stack safety. Do not
hoist admission across observable effects, add a guard framework or force a
larger lifetime redesign. A selected prototype must name the code removed and
justify any conservative nesting tradeoff before timing complete scripts.

Existing F4as_v7 Ubuntu/.NET11RC3 collection tooling captures two six-second
windows, 11.644 seconds in total, with normal GC/tiering plus recorded perf/JIT
instrumentation. Actual loaded hashes, output/files, ownership, quiet gates,
joined users, RAM cleanup and persistent archives all pass. B591 exits 0 and is
terminal/cached. Independent retrieval verifies the archive. Only obsolete initial
R245 core aliases are retired, after exact committed canonical archive-entry and
fresh path/UID/inode/all-alias proofs; the current refined image is protected.

[Assessment](assessment.json), [raw code/maps/profiles](observations.tgz),
[identity](identity.json), [source hashes](source-identity.json),
[receipt](archive-receipt.json) and [archive hashes](archive-identity.json)
preserve all evidence. A generation assertion and reused staging filename stop
before VM work; corrected preparation and original conditions survive. Full
delivery is independent: original 43081976930 failed on a Windows .NET 10 callback
deadline; corrected 43087005596 is separately tracked. Current comparisons belong
in [BENCHMARKS](../../../docs/BENCHMARKS.md). Publication remains postponed.

Subsequent corrected delivery `43087005596`, source `458be840`, is **GREEN** on
Windows/Ubuntu, actual .NET 11/10, original stress and installed Release consumers.
Its production source is exactly retained `8f8628a4`; the correction changes test
scheduling, keeping five-second deadlines and every assertion. The
[terminal receipt](delivery-ci-status.json) is cached. Original failure and
collection-time status remain preserved. This does not qualify later prototypes
or resolve the report CPU debt.
