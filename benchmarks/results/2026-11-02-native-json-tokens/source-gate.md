# B122 source gate / B123 native JSON token construction

Baseline217ebb65/runtimec3fa26cc; reuse B119 report profile, no new capture.
ReadJsonString347 inclusive samples (54 self), PyString.FromUtf840 self, overall
JSON571/1728 inclusive;279 monitor self span many paths. These overlap and do not
establish a removable percentage. Strings/literals already avoid async scalar
helpers; valid escapes already use UTF-8 CopyString. A parser-stack/default-async
rewrite would add storage and is not justified ahead of the demonstrated token
work. PyString is byte[] storage and slices copy; no imagined existing zero-copy
view can be used safely without a larger representation/ownership change.

Current scan already determines boundaries, escapes and bare controls, but throws
away the first two facts. Every plain token is then parsed again by Utf8JsonReader
and gets a4x temporary reservation even though the plain branch only copies final
UTF-8. Record closure/escapes/strict controls in the existing scan. For closed,
unescaped, valid UTF-8 with no forbidden control, construct exact PyString storage
under the existing governor directly. Lenient plain controls are already valid
native content, so no rewrite is needed there. Escaped/invalid/unterminated cases
retain genuine backend validation, scratch and original-coordinate translation.
Partition before allocation/admission; never recover from refused admission.

Bounded scan checks retained, plus active checks before/after output creation.
Final capacity+metadata preflight remains, sources/escaping aliases still funded,
and cleanup independent of GC. No new state stack, format/lexer framework, view,
cache, coupon, allowance, configuration or owner. Tiny-budget thresholds can
improve by removing unused scratch, not by dropping final/working storage charges.

Focused Release JSON/raw_decode/strict/Unicode/error/hook/duplicate-key/stack and
memory/cancellation checks on actual.NET11/10. Preserve existing assertions; new
coverage only if a genuine path is missing. One two-block complete JSON-to-report,
JSON-lines/helper, CSV/text control comparison with independent unchanged controls;
existing short complete-job allocation diagnostic. Preserve all adverse/failed
conditions. Outcome can retain with documented uncertainty, refine with evidence
or withdraw. Do not call this a universal parser redesign or objective completion.
