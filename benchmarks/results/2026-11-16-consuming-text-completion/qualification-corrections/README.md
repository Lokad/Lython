# Test-only qualification corrections

Initial full CI **43797131423 / 116fe6e0** failed on both platforms at .NET11:
six internal failures, 4,771 passes. Ubuntu public: 17,914 passes. Windows public:
17,913 passes and one process-allocation assertion failure. .NET10, stress and
installed package stages were not reached. Full original logs are retained.

Three tests enforced the removed final-copy/exact-charge architecture. They now
check funded retained capacity, actual metadata/output refusal with live operands,
first terminal attribution and complete refunds. Removed private-helper reflection
and growth-specific assertions; primitive replacement-overlap coverage remains.

The Windows JSON separator test observed 168,440 process-allocated bytes against
a 128 KiB bound. It now warms and compares the same small/large constructor paths,
testing size-dependent copying against the unchanged 128 KiB bound. This avoids
charging fixed startup work to separator size; no conclusion about the original
extra allocation's source is inferred. Nonparallel GC collection remains.

Focused Release: corrected classes 13 internal / 8 public, broadened affected set
100 internal / 2,555 public, each actual .NET11/10. All pass. Production core stays
**810dafdd**; only four tests changed. Original timing/source images remain immutable.
597 production/build/test inputs frozen for the new delivery; no retiming needed.
Full cross-platform/stress/package qualification remains pending. No memory bound,
timeout, runtime policy or containment contract was relaxed.
