# Bounded UTF-8 floating-parser entry assessment

Assessment at development **81c29a11**, 2026-11-01 Paris / 2026-10-31 UTC.
**No production change or whole-script speedup claim.** This is a finite component
check selected from the existing [B13 call-site attribution](../2026-10-31-numeric-cpu/README.md#subsequent-offline-float-call-site-attribution-b13):
106/380 guest samples fall under the current CLR UTF-8 float parser boundary.
That older capture identifies the boundary, not a removable CPU fraction; no new
VM capture or retiming of completed/rejected candidates is performed.

Current callback-free JSON uses invariant UTF-8 double.Parse with NumberStyles.Float
and a completed ValueTask. There is no UTF-16 conversion or general async state
machine problem in this branch. JSON grammar is validated by Lython beforehand.
Hypothesis: a public UTF-8 parser entry may avoid general format/culture work.
This does not justify a custom floating parser, universal tagged values, numeric
quotas or removing cancellation/admission protection.

## Fixed corpus and comparison

The [frozen corpus](tokens.json) contains **2,137** unique tokens: every unique
float token in the existing complete float-report input plus explicit halfway,
large-magnitude, subnormal, signed-zero, overflow/underflow, exponent and long-
mantissa cases. Expected IEEE binary64 bits are independently generated with
isolated local **CPython 3.13.2** (`struct.pack('>d', float(token)).hex()`). The source
catalog/input is preserved in the [B17 raw archive](../2026-11-01-native-utf8-errors/raw-vm-results.tar.gz).
This finite corpus is useful acceptance evidence, not exhaustive equivalence.

The [standalone existing-API driver](Program.cs) checks both current
`double.Parse(ReadOnlySpan<byte>, NumberStyles.Float, InvariantCulture)` and
`System.Buffers.Text.Utf8Parser.TryParse` against every golden before timing.
It requires complete token consumption. Both match every bit pattern, including
signed zero and infinities. No failure fallback or extra parser branch is added.

Pre-code gate: exact corpus/consumption plus >=20% lower standalone median before
considering a separate whole-script prototype. Four warmups, seven alternating
rounds of sixteen corpus passes, one owned 30-second process; it finishes normally.
Token encoding/corpus construction, compilation/startup and result serialization
are outside timing. No host I/O, Lython execution/frame costs, admission calls or
cleanup costs are included. Both loops use identical pre-encoded input storage.

Windows 26200 / 28 logical processors, pinned .NET 11 RC3, ordinary GC with tiering
equally disabled for this **local component diagnostic**. This differs from VM
workers, which keep ordinary tiering. Every raw time and any semantic mismatch
is retained in [result.json](result.json); [project](float-entry.csproj).

| Sixteen corpus passes | Median elapsed |
| --- | ---: |
| Current invariant double.Parse | 1.6584 ms |
| UTF-8 Utf8Parser | 0.9450 ms |

Candidate/current = **0.5698**, or 43.0% lower in this component observation.
This passes the assessment gate and only motivates the next bounded complete-
script prototype. It supplies no current CPython multiplier, guaranteed runtime
speedup or supported .NET 10 qualification. Compilation, host I/O, cleanup,
parse_float hooks and shared ownership were not exercised by this component loop.

A production proposal must preserve JSON grammar/error locations, exact rounding,
signed zero, overflow/underflow, complete consumption, original callback tokens,
current cancellation and terminal refusal, admitted retained input/output and
both actual target runtimes. Require whole-script CPU/elapsed controls before
acceptance. Do not infer overall gains by multiplying the old sample fraction by
this microbenchmark ratio, or let missing regex producer releases block the work.
