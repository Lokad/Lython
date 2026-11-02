# B93 native string-format pipeline — before code

Source base c3a0eb1a. B92 measured78/273 formatter-rooted guest samples (28.6%),
not a removable/string-only fraction. Selected structural hypothesis: specified
string width/precision still decode the full subject to CLR UTF-16, slice/pad CLR
strings, then encode UTF-8, even though data and native padding already use UTF-8.

Change FormatInterpolatedStringValue to return PyString directly. Keep parser and
validation/error order, numeric/complex/date/custom formatting. Replace only its
string rendering branch with native scalar-precision/UTF-8 padding. Share existing
padding builder/fill loop with str.ljust/rjust/center, preserving format^ floor
centering versus str.center parity. Reuse governed builders, capacity/overlap
preflights and owners. Generalize the existing unpublished casing-completion guard
by name/token only; never refund borrowed aliases. Old casing behavior stays.
Delete subject AsString, UTF-16 precision slicing and CLR string padding/re-encoding
for string subjects. Numeric CLR formatting/padding remains where it has callers.
No tagged values, new backend/provider/owner layer, public options or policies.

Preserve Unicode scalar precision, multibyte/zero fill, defaults/zero alignment,
error order, !s/!r/!a and nested specs, custom __format__ effects and real async
callbacks. Native scanning/fill checks current token; final funding observes
cancellation/first refusal and refunds only unpublished output, never the input.
Precision-only copy/padding storage is admitted before allocation; existing input
and output capacity overlap remain funded. Smaller conservative charges are an
internal estimate change, not a promised tiny-budget threshold or new cap.

Focused Release semantic/format/protocol/casing/governed-memory/cancellation tests
on actual .NET11/10; independent CPython goldens for Unicode/all three entry APIs,
odd alignment, errors and nested conversions. Native phase budget tests trigger
refusal/cancellation by observable host admission, preserve aliases and return to
baseline without relying on exact checkpoint counts. Existing held callback and
retained-owner tests remain. No relaxed containment assertion or timeout.

One short affected warm report-render.large old/new/CPython diagnostic and one
unchanged JSON-helper.large control, using maintained micro command, 15s collection
and30s owned service per command. Frozen exact baseline worker is already saved;
copy its common benchmark/adapter/auxiliary files for candidate, replace core only.
Normal GC/tiering, full input/stdout/file goldens, owner cleanup, input identity and
pre/post quiet gates; build/test/transfer before timing, shut down owned SDK servers.
No new profiler capture, ten-minute lane or private comparison framework.

Retain if structural/semantic/ownership gates pass, report elapsed point median
improves>=7.5%, JSON control regresses<=5%, both Lython series have IQR/median<=15%,
and both short comparisons complete with joined cleanup. These remain DIAGNOSTIC
point estimates, not qualified speedup/CPU shares. One observation each, no retime,
post-result gate relaxation or fragment retention. On failure withdraw all four
production files, rebuild nonincrementally and retain independent regressions.
If retained, update current runtime docs where needed and qualify full Windows/
Ubuntu ordinaryboth targets/originalStressboth/installed Release consumers once at
the coherent runtime boundary. Published0.10/tag and adapter pin0.4 stay unchanged.
