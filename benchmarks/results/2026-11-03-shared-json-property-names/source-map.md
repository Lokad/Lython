# B138 source gate, 2026-11-03

Baseline 1e73f72f, runtime fd7bc7e4, baseline core SHA256
547825f9dfe52ddf22e24e76b8bc54ae8975428dae5b5e22052dd223624d2c42.
ReadJsonObjectKey delegates to the ordinary token decoder. Its plain branch always
CopyFromUtf8, allocating/funding a new PyString and backing; then PyDict hashes it.
JSON-lines recreates the same few keys on every loads call. A per-parse table would
lose sharing across flat records and add per-record setup. ExecutionState is already
shared by reentrant calls/imports; MemoryGovernor is that same execution/result
owner, so no new ownership boundary is needed. JsonModule.Instance is global and
must not hold this mutable state. Results retain batch funding already.

Use a lazy Dictionary<ReadOnlyMemory<byte>,PyString> with safe BCL HashCode.AddBytes
and SequenceEqual, stored only over owned immutable decoded key bytes. Borrowed
input is lookup-only. Existing decoder handles escaped/invalid/control semantics;
only fresh unpublished escaped duplicates may be refunded. No native/unsafe API.
A conservative flat entry charge covers hash-table capacity and old/new overlap,
matching the existing coarse capacity policy without an exact growth ledger.

Unique keys add retained index capacity and can deny earlier. This is an explicit
tradeoff to measure, not a silent assumption of repeated schemas. No public option,
cap, token retention, global pool, weak references, general interner or guest tracing.
No budget locks are removed: required shared allowance synchronization is real.
The current profile shows storage/admission callers, not an exact removable share.

Python identity permission: external/cpython/Doc/reference/datamodel.rst lines
102-114, immutable operations may reuse an existing object with same type/value;
mutable object constructors must return distinct objects. No string identity promise
is introduced. Existing scanner checks long tokens; BCL hash/copy operations, like
current PyString hashing, do not offer mid-operation cancellation.
