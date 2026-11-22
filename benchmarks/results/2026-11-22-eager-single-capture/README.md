# Combine eager annotation and first capture admission

Private `30c1632cf` extends the combined callable and first-local-cell
reservation to definitions with direct eager annotation reads. Annotation
evaluation precedes publication. The preflight can refuse the combined
capacity with an earlier terminal memory failure. The same source span
still identifies the definition.

The reference `32c50290d` and prototype pass the same four CPython
output goldens. The prototype passes 611 focused Release checks on Windows
.NET 11 and Ubuntu .NET 10. The cases include eager-name failure,
retained aliases and correct refund when a definition fails.

All five local representative scripts make the same number of memory
requests with matching allocation. The eager nested factory captures
its local in the outer definition and evaluates annotations in the
inner one. The candidate does not remove a reservation on that workload.
Keep it private. This diagnostic does not measure elapsed or CPU.

[Local requests and allocations](local-reservations.json) ·
[Source, images and checks](procedures.tar.xz).
