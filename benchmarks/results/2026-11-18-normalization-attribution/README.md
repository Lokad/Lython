# Text normalization attribution

R553 profiled the 4,096-line text normalization script on Windows with the
retained `93739eed` core and all 32 worker files verified. The worker used
ordinary GC and tiering, three seconds of warmup and six seconds of managed
thread sampling. Nineteen measured batches matched stdout and output file
goldens. The worker joined and released its fixture.

Of 469 managed samples, 466 resolve to Lython or the comparison host.
The nearest host-file group accounts for 264 samples. Fixture reset and
output collection alone account for 164 nearest frames. Within the runtime,
21 nearest frames are at `DrainLeased`, 19 at `BuildSplitResult`, eight at
byte replacement, seven at `ExpandTabsUtf8`, and seven at `RStrip`.
Inlined operations and unresolved stacks limit attribution. These counts
include work outside guest execution and do not estimate removable CPU.

The script first splits the full text, trims each line in a generator, then
joins the generator. The exact `join` still retains all yielded parts
through output admission. This preserves subsequent iterator errors and
effects, including when an earlier item has the wrong type. Any combined
path must preserve that behavior or prove its input and producer are private
exact text for which those effects are absent. Preserve Unicode, checked
capacity, cancellation, refusal, alias funding and the generic iterator path.
The current local trace supports profiling the split/trim/join region inside
the RAM-backed guest boundary. It does not justify a representation change.

[Sample attribution](attribution.json) · [Capture](capture.json) ·
[Trace and source procedure](trace-and-procedures.tar.xz)
