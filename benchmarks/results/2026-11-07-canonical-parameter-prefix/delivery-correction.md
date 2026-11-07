# Shared-layout stress contract correction

Delivery42339537256, source42643b1b, is terminal failed on both platforms. Release
builds, ordinary actual.NET11/10 suites and installed package consumers all pass.
Original.NET11 Stress:3internal/405public pass, one public failure. .NET10 Stress
is not reached. Terminal receipt and full failed logs are preserved; never
re-query this run or present its partial gates as full qualification.

The sole failure requires5000discarded40-default functions to exceed20MiB. Its
original comment explicitly bases that threshold on per-function parameter maps/
arrays, which R31 correctly removed. It now fits. This is an obsolete mechanism-
specific denial threshold, not evidence that retained defaults lost their funding.
The original retained-function/lambda6MiB refusal tests pass on both platforms.

Keep all original5000-definition scales. Replace the obsolete20MiB refusal with
6MiB default-storage refusal and replace the redundant64MiB success case with
20MiB success. Both positive/negative coverage remain; the success allowance is
stricter. No runtime charge, timeout, stress count or containment assertion is
restored/relaxed to make CI green.

All scenarios now compile under the same borrowed root, pin real shared-admission
failure/requested bytes, return to the compilation baseline after result cleanup,
reuse the allowance for the next API and return to zero after program disposal.
The corrected entire scenario class, including all four original stress cases,
passes8tests per actual.NET11/.NET10 in0.52/0.60seconds. Every original condition,
patch and log survives in defaults-stress-correction.tgz.

Correction is qualified locally alongside the R33 native-hash implementation,
which is committed separately asabebd004. Batch both into one corrected full
Windows/Ubuntu, ordinary/Stress/installed-package delivery. Previous green gates
remain historical evidence; no complete delivery is claimed until that run passes.
