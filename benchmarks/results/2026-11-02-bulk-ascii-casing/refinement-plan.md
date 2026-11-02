# B118 one bounded refinement

Original source/images and all twelve observations are complete/preserved.
ASCII file CPU -39.1/-38.7% versus baseline, controls-4.4/-0.2%; report elapsed
beats controls about3–5%. Unicode-only CPU+7.28/+3.41%, controls0/+3.05%:
first adverse point is meaningful, not erased or labelled a failed design.

Current loop checks execution per Unicode rune but per ASCII prefix. Consolidate
the cadence at the original-subject byte-window boundary: <=4096 bytes plus at
most one complete Unicode scalar. ASCII consumes only the current window; its
helper no longer needs its own window limit. Sigma context scans keep their
existing cooperative checks. Builder operations retain admission refusal checks;
the final check before publication remains. No timer/fuel/new policy/config.

Preserve all original Unicode expansion/title state and source offsets; no extra
mapping backend or new storage. Focused cancellation/denial/ownership/Unicode
goldens per actual runtime first, then the same twelve source-pair observations
with unchanged controls. This is one source-backed refinement; decide retention
and remaining regression debt afterward, no open tuning campaign.
