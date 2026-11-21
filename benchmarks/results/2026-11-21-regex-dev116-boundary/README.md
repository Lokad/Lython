# Utf8Regex dev.116 package boundary

Producer source `4b6446ebb` and the immutable 0.5.0-dev.116 packages.
The direct Windows probes verify both package and assembly hashes for
Core and PythonRe on actual .NET 10 and .NET 11. The probes copy a UTF-8
capture name and pass an ordinary anchored match. The reversed anchored
window still raises an out-of-range error at startOffsetInBytes.
CPython 3.13.2 reports an empty whole match and participating capture
at the independently clamped start.

Core construction estimates at this frozen revision still use
Unsafe.SizeOf. The optional adapter stays pinned to released 0.4.0.
This preview check covers exact artifact identities, capture-name
transport and the known window gap. Final adapter qualification still
requires a managed release with correct windows and in-flight
cancellation. Prior qualification is linked separately.

[Candidate manifest](producer-candidate.json) · [Direct checks](checks.tar.xz)
· [dev.115 downstream replay](../2026-11-21-regex-dev115-boundary/README.md).
