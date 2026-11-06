# R03 refinement after B332; before rebuilding

B332 is terminal/archived,85.92s/three complete jobs/all gates. Warm text
candidate/control2.356/2.333ms versus production2.272ms: +3.69/+2.69%.
Fresh2.604/2.440 versus2.406ms: +8.21/+1.38%,6.31% identical-role spread.
CSV unchanged control4.676/4.769 versus4.813ms, -2.83/-0.90%; no CPU attribution.
Keep all original samples, initial failed disk checks and fixed worker identities.

Native SearchValues lookup may outweigh scalar decoding for frequent short words;
this is a hypothesis, not a demonstrated explanation. Refine inside the same
milestone: remove both SearchValues tables, metadata construction and library
search dispatch. A small byte-category run loop shares forward/reverse discovery;
the common ordinary/cased planner remains. No new fast-path selection or option.
Retain64-byte cancellation boundaries, Unicode fallback, complete output admission,
case capacity, Python inventory/ordering, aliases and cleanup. The existing focused
checks and six independent Python boundary goldens remain acceptance.

Repeat the existing three complete jobs once, against the original794e552b
production with two identical current roles; preserve B332 alongside refinement.
Choose retain/withdraw after simplification and complete-script regression debt,
not a fixed speedup threshold. No allocation/CPU prediction and no broad campaign.
