# Ordinary call construction source gate

2026-11-04. Read-only diagnostic on source0ffc9ac6/frozen0ee77cd0, actual Windows
.NET11RC3. It does not measure CPU/elapsed improvement or the later copy-context
correction. Three isolated images of the same core share CoreLib/process GC;
all CPython goldens, hash witnesses, owner balances and fixture cleanup pass.
Reuse the existing complete-job allocation tool; no runtime instrumentation,
configuration option, forced GC or new benchmark framework.

Six checksum scripts pair a native leaf helper with a conditional ordinary helper
at128/512/2048 calls. Both compute the same value+1 and result; the ordinary helper
also compares/branches. Warm execution excludes compilation and includes setup,
output checks and cleanup. Five rotated16-job samples after32 warmups/image.

| Shape |128 calls|512 calls|2,048 calls|Allocation slope |
| --- | ---: | ---: | ---: | ---: |
| Native leaf |33,235B|70,107B|217,563B|about96B/call|
| Conditional ordinary helper |102,867B|348,649B|1,331,703B|about640B/call|

The three independent images agree within rounding. Difference: about544B/call,
including extra comparison/truth work. It is not all frame allocation and is not
CPU attribution. Invocation overhead is approximately21KB in this diagnostic.
[Exact summaries](summary.json), [sources, samples and existing driver](allocation-evidence.tgz).

Source facts: namespaces are already lazy for ordinary slots; argument-presence
flags use an inline word, bound locals already share temporary storage, and
ExecutionContext inherits ExecutionFrame as one object. Ordinary functions still
construct an invocation context, interpreter and funded value array. The earlier
B193 caution about surviving fields/lifetimes remains valid.

## Complete-script CPU attribution

The existing Linux perf collector records the complete text summary with its
original `words_from_line` helper, then with an output-equivalent empty-line guard
that selects ordinary executable function frames. Both use the SAME frozen core;
this compares source shapes, not an optimization or CPython performance.

Quiet Azure F4as_v7, Ubuntu 24.04.4, .NET 11 RC3, normal GC/tiering. Six seconds
warmup and 2,048 complete jobs per shape; mediated RAM-file I/O, full output/file
verification and result cleanup included. Only the diagnostic CLR symbol map is
enabled. Both quiet gates, verification, normal worker Close/exit and fixture
cleanup pass. Captured batches last 6.258 and 7.387 seconds; named guest CPU is
6.19 and 7.33 seconds. Instrumented shape differences are not latency gains.

| Diagnostic samples | Native helper / 1,231 | Guarded helper / 1,456 |
| --- | ---: | ---: |
| Instruction-prefix leaf | 199 (16.2%) | 290 (19.9%) |
| Monitor enter/exit leaves | 61 (5.0%) | 133 (9.1%) |
| Construction inclusive chains | 2 (0.2%) | 65 (4.5%) |
| Binding/rent/return inclusive chains | 0 | 51 (3.5%) |
| Lower/split inclusive chains | 277 (22.5%) | 264 (18.1%) |
| Unknown leaves | 73 (5.9%) | 86 (5.9%) |

Inclusive groups overlap and exclude inlined/unresolved ancestors; do not add
them or infer removable CPU fractions. Monitor samples do not identify only
frame admission. Each map has one incomplete trailing symbol line, excluded by
the parser. Raw stacks and unknown/native frames remain in the archive.

[Raw CPU evidence](cpu-observations.tgz), [mapped leaves](cpu-leaves.json),
[inclusive attribution](call-chains.json), [offline reader](analyze-call-chains.py).
CPU archive SHA256:
`53dcf02744c17263b972061b2b974b0590dec60876c86c6186513767652ee8bf`.
To reproduce inclusive attribution, extract the CPU archive into `retrieved/`
beside the reader, then run it. No runtime diagnostic code is installed.

## Design decision

This closes the bounded call-construction investigation. Ordinary calls allocate
material storage and the guarded complete script does more work. The evidence
does not identify a dominant, removable construction mechanism that justifies
another general frame rewrite. Keep the existing stable contexts and executable
frame chain; proceed to bulk storage/text source costs before selecting that
rewrite. This is a priority decision, not a permanent rejection of frame work.

Reopening requires a concrete lifetime/deletion design removing complete working
storage or transport, rather than another arity path or shifting hot fields into
saved records. Preserve the measured slower arena/control/reusable-dispatch
lessons, actual lexical and suspended users, defaults/errors, cells, cancellation
and admission. Any future selected redesign needs complete-script controls.
