# Lookup request size correction

The initial plan called JsonNameLookup40 bytes, mistakenly treating LythonSourceSpan
as a16-byte struct. It is a sealed record class. The request field footprint on64-bit
is32 bytes: ReadOnlyMemory16 + governor reference8 + span reference8. Original
plan snapshots/source/results are retained. The refinement removes the complete
request; reduced CPU cost remains a hypothesis, not an attribution established by
that arithmetic. No runtime/source/measurement changes follow this correction.
