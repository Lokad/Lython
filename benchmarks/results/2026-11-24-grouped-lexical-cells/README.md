# B1562 grouped lexical capture storage

The private prototype groups contiguous lexical locals when a native
activation first creates a capturing closure. Sibling functions share
one admitted backing and reuse the compiler's capture map. Forwarded
bindings retain their original owner and index. Promotion to an
ordinary frame preserves those bindings. The group stays charged
through execution ownership, including slots that a particular child
never reads.

| Warm script | Elapsed change | Process CPU change | CLR allocation change |
| --- | ---: | ---: | ---: |
| Captured rules | +6.59 to +10.03% | +7.27 to +8.93% | −9.88% |
| Default rules | −0.62 to +1.63% | +0.33 to +3.68% | +3.35% |
| Annotated rules | +9.02 to +22.38% | +8.91 to +22.62% | +3.35% |
| Quoted invoice | +0.96 to +2.21% | +1.28 to +2.00% | +88 bytes |

Captured rules use 147,440 fewer CLR bytes and 16,384 more peak
admitted bytes. Current admission conservatively funds all four
contiguous slots together. Other admitted peaks match. All allowances
return to baseline. The extra capture-map pointer increases every
function object's size, including functions that do not group cells.
The annotated candidate workers differ by 11.01%, making the size of
that timing penalty uncertain. The captured-rule and quoted-invoice
worker spreads stay below 1.8%.

Keep this snapshot private. A compact follow-up can get the capture
map from the current code object, keep ordinary function objects
their old size and avoid group inspection during general scalar
publication. Measure the same captured and control scripts before
deciding whether to extend the design to ordinary framed entry.

The frozen reference `5d54f7afb` matches the runtime and tests in
`566259ebc`. Only the core DLL differs in the candidate `5560f12f5`'s
32-file image. The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11
RC3 on November 24, 2026 UTC. Two workers per source warm for eight
seconds and contribute seven rotated samples. Ranges span all
pairings of their medians. Warm elapsed includes guest execution,
mediated RAM-file I/O where applicable, verification and prior-result
disposal. Process CPU also includes reset and request overhead.
Compilation, startup and final teardown are outside warm elapsed.
All 112 batches match their CPython 3.13.16 goldens. Both quiet gates,
worker joins, fixture cleanup and persistent verification passed.

Windows and Ubuntu each pass 151 white-box and 784 public .NET 11
Release checks. An initial Windows child process could not locate the
pinned .NET runtime. The same selection passed with its inherited
runtime environment corrected. The focused async case checks sibling
captures, a forwarded nonlocal write through a suspended factory,
late rebinding, cancellation and refunded ownership.

[Timing](summary.json) · [Allocations](allocation-summary.json) ·
[Image identity](identity.json) · [Raw measurements](observations.tar.xz) ·
[Procedures and exact patch](procedures.tar.xz) · [Terminal receipt](terminal-status.json).
