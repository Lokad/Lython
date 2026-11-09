# Current operand transport source assessment

R147,2026-11-09; corrected58d3ec0f runtime source. Reuse the existing compile-only
metadata census for five maintained CSV/text/JSON/report/search scripts. No guest
execution, production instrumentation, counter policy or runtime changes.

All five module bodies and listed helpers are nonmirrored. Their current compiler
already forwards constant/uncaptured-local inputs and marks general producer/local
results. The runtime skips ordinary temporary result publication and the following
store dispatch. Retain that store as the pending-disposal continuation: cleanup
must settle before binding publication, with the original terminal check and span.
Replacing it with new metadata does not establish CPU value or another removable
value-transfer phase. These static counts are not dynamic execution frequencies.

The same source gate confirms direct final UTF-8/managed bulk ASCII casing, fused
final-word splitting and coarse small-integer admission already exist. R140/R143's
VM-prefix shares include useful work; they do not justify treating those phases
as absent or predicting a universal speedup. No new dispatch/arena, second capture
or unchanged retiming selected. This is a completed bounded gate, not a permanent
ban on an evidence-backed redesign. The broader performance objective remains open.

[Summary/source findings](summary.json), [exact census, sources and diagnostic](evidence.tar.xz).
