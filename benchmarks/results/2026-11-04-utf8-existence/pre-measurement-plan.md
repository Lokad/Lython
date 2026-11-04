# Constant-time native UTF8 existence checks

Before collection,2026-11-04. One source pair: main97bc43b2 module-facts source,
frozen0d452f95, vs same source with archived string-truth/text-file EOF patch.
No other runtime change, no new threshold or exception policy. Use strict UTF8
logical byte extent for truth/EOF; count readlines characters only for positive
hints. Scalar length/index semantics unchanged.

Initial catalog was NEVER timed. Source inspection showed str.count and startswith
already demand scalar bounds, masking an existence-check cost. Retain the untimed
catalog; final whole scripts count chunks/lines containing a marker using native
substring membership (no scalar count needed). ASCII and Unicode/NUL input files,
complete1MiB mediated RAM reads, checks/stdout/result disposal; compilation separate.
Existing complete text helper warm and config-edit fresh cases are controls.
No cached-string microbenchmark or CPython timing claim.

Use existing three-isolated-image complete-job driver with only frozen-case guard
changed: normal GC/tiering,6sec guest warmup/image, alternating order,2sec settle,
9rotated200ms batches calibrated from baseline only, three roles including same-
source control. Exact bits/CoreLib/hash/CPython goldens/owner baselines and final
zero/fixture cleanup/quiet gates required. Persistent evidence, RAM fixtures only.
One163s-ish collection under240s owned bound. An observation timeout is not design
failure; retain original/adverse data and confirm only if material uncertainty
justifies it. Decide architectural retention separately from timing magnitude.
Focused Release semantics, positive hints, cancellation, refused admission,
retained files/results and shared allowance reuse on actual11/10. No new mirrored
internal test or performance switch. Full platform checks batched at delivery;
prior D5 checks already green and never replayed for that unchanged source.

Setup corrections before ANY timed job: generated shell CRLF rejected by sh;
then Windows CPython print goldens contained CRLF rather than Linux LF. Both
launch failures retained. Fixed shellLF and normalized only new stdout goldens;
source/fixture hashes, images and sampling policy unchanged.
