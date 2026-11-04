# B190 source decision — shared numeric layout and final-window transport

Baseline runtime9ce21533, HEAD46823214. Previous goal turn made real progress:
B188 source/tests committed and B189 independent external/coupled observations retained.
B188 full delivery41742152264 still running original Stress; six ordinary/package gates green.

Demonstrated bug: general/default/percentage float and Decimal group before adding
zero fill; independent CPython probe retained in B188 evidence. Native F/E/integer
writer already correct and must stay shared.

Selected migration, before source edits:
1. Extract the existing numeric layout arithmetic into one small immutable value
   (no owner/cache or policy). Both byte-span and real CLR numeric text consumers
   use the same admitted final window and existing grouping/zero/padding writer.
2. Real text renderers return ungrouped ASCII. Encode raw text at the END of the
   final window, then write forwards with the shared writer. Every digit's final
   position is <= its staged source position: remaining insertions/trailing fill
   only move its source right. Overlapping Span.CopyTo is allowed; grouping reads
   each byte before writing it and never overwrites a future source byte.
   No proportional encoding buffer or new CLR padded/grouped string is required.
3. Complex nonempty formatting appends components through the same numeric writer
   using the existing governed builder mechanism in a private working lifetime, then pads/copies the UTF8
   body to the parent. Keep both capacities funded through copy; release private
   builder on all exits. Preserve real empty-format Render(), null-type .0 rules,
   signs, imaginary-only/negative zero, grouping, alternate form, rounding and
   errors. No callbacks or async users are invented for native complex values.
4. Remove numeric branch of AppendInterpolatedFormatPadding, GroupDigits /
   GroupFloatingDigits / orphan FormatNumericSign / prefix-out plumbing. Complex
   padding uses UTF8 directly; only actual text rounding algorithms remain.
   Remove FormatSimpleFloatingText and finite F/E text arms once no real caller
   remains. Keep direct finite F/E span algorithm and genuine protocol boundaries.

Affected work: report fields, Decimal/report helpers, complex formatted output.
Allocation hypothesis: remove pre-grouped CLR assembly and complex component/body
strings; no prediction that this closes whole-script CPU gaps. Shared layout tuple
or private value record has no retained storage. All major final/private capacities
and replacement overlap use existing governor; admission refusal terminal, aliases
and cleanup preserved. Native invariant output is ASCII; fill still Unicode scalar.
No change to public options, stack policy, host capabilities or cancellation control.

Validation: independent CPython goldens for legacy grouped zero fill and complex
forms, all three formatting entry points/both APIs and budget baseline reuse;
existing rounding/format/protocol/suspension/refusal/alias tests on actual11/10.
Add focused denial controls for substantial widths, not checkpoint-count tests.
One short affected numeric/complex and complete report comparison with unchanged
controls, both APIs, separate allocation proof. Preserve adverse/moving results;
bounded confirmation only if justified. Full platform/package at a coherent retained
boundary, never a parallel full local suite. No extra CPython campaign.

Completion: one layout/byte output invariant, displaced assembly removed, correct
legacy cases, no unexpected admitted peak/failure behavior, explicit measurement
and debt decision. Then return to whole-script calls/frames/per-record roadmap;
fresh-JSON debt stays provisional2–5%, unrelated to this formatter correctness fix.
