# B1341 Compile read-only parameter captures as callable values

Private `6cb1da6dc` proves whole read-only capture layouts for named
lexical functions with no forwarded incoming binding. Emitted loads
read a singleton or an array directly. The callable carries this
storage in its existing capture field. Mutable bindings and namespace
consumers keep their cells. Where a sibling still needs a cell, the
new capture shares the object and owns a conservative scalar estimate
through the same execution allowance. Decorator and annotation order
remain on the existing preflight path.

Compared `2d4fb517b` and `6cb1da6dc` on November 22, 2026 UTC on the
quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. Both revisions
used two workers per phase, at least twelve seconds of warmup per
worker and seven rotated batches each. Ranges span all four pairings
of process medians. Warm includes output validation, result cleanup
and mediated RAM-backed reads and writes for the invoice. Compilation
includes code-owner disposal. Time to result combines compilation,
one run and cleanup. Process CPU covers the wider request. Startup
and final teardown are separate.

| Complete script | Phase | Elapsed change | Process CPU change | Elapsed spread reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Fully supplied helper | Warm | −1.88 to +2.74% | −2.85 to +2.77% | 2.56 / 2.09% |
| Eager nested factory | Warm | −2.85 to −0.30% | −1.65 to −0.20% | 0.34 / 2.28% |
| Mixed literal-default factory | Warm | −4.33 to +2.15% | −5.07 to +1.87% | 3.75 / 2.91% |
| Default-observing factory | Warm | +1.10 to +5.91% | +0.55 to +6.43% | 0.14 / 4.61% |
| Postponed nested factory | Warm | −3.45 to +2.33% | −4.39 to +1.72% | 0.81 / 5.14% |
| Plain integer invoice | Warm | −4.03 to −0.19% | −3.85 to 0.00% | 3.92 / 0.07% |
| Mixed literal-default factory | Compilation | −1.36 to +1.21% | −2.58 to +0.55% | 1.48 / 1.11% |
| Mixed literal-default factory | Time to result | +2.94 to +5.49% | +2.71 to +5.06% | 0.65 / 1.81% |

Keep the prototype private. The mixed factory allocates 10.19% less and
the eager factory allocates 4.29% less. Independently collected mixed
time to result uses 2.71–5.06% more CPU and takes 2.94–5.49% longer. The
mixed warm and compile-only ranges span zero. Eager warm is modestly
faster. The default-observing factory is slower in all pairings, with a
wide candidate spread. The postponed and fully supplied comparisons
span zero. The source proves less capture backing and the measure
establishes allocation improvement. The first-use result warrants
finding a path that also removes runtime staging or repeated definition
work before promoting this layout.

Windows .NET 11 and Ubuntu .NET 10 each pass 1,631 white-box and 3,673
public focused Release checks. Both images match seven complete-script
goldens plus an independent CPython case for aliasing, loop and exception
rebinding, deletion, comprehensions, imports, generator escape and a
24-name capture. Shared allowances refund in every measured allocation
lane. Mixed admitted peak falls 131,072 bytes. Eager, postponed and
default-observing peaks each fall 65,536 bytes. The helpers and invoice
keep their prior admitted peaks.

The original collector completed seven phases and stopped before the
eighth on a copied overall cutoff. It had a quiet entry check. Its
original after-check is absent, so those seven timings are diagnostic.
All 196 recorded batches verify and every worker joined. The separate
75-second time-to-result lane used the same 32-file images, sources and
goldens. Its quiet entry and exit checks and 28 batches pass. The
original failure and data are preserved alongside completion. The
persistent archive verified under the lease and the empty fixture
mount was removed. Initial test setup failures from a sparse checkout
and outdated internal fixture constructors are included. No corpus
case or affected control was excluded.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Sources and checks](checks-and-procedures.tar.xz).
