# B158 native UTF-8 handoff source gate

No runtime prototype yet. B15793bc77bb is retained and qualification runs separately.
Concrete remaining work: CSV WriteRowCoreAsync consumes its governed builder into
an owned PyString before a native file copies it; native JsonEncodeIterator also
copies each chunk into a PyString before writing. Public/custom outputs require
those independent values. Exact owned native files can consume a bounded borrowed
UTF8 range synchronously before existing buffer reuse/release.

TextFileWriteState.Write currently uses a PyString for scalar count, UTF8 bytes
and strict single-byte codec errors. Its existing SingleByteEncodeError accepts
a nullable original and already admits/copies raw UTF8 only on error. A shared
UTF8 write core can preserve original object identity for normal Python writes,
and reuse existing lazy error input for unpublished native records. Keep one
codec/newline/BOM implementation; no encoding-specific alternative backend.

Native JSON writer failure is different from encoder failure: an escaped encoder
must remain resumable after a consumer write error. Newly added public
NativeWriterFailureLeavesCapturedEncoderResumable first exposed a frontend
rejection: class-local fact cloning hid a method's global assignment. Corrected
by applying the existing recursive directive invalidation to the parent before
cloning. The new test and nested-class global regression now pass: 214 focused
public cases per actual .NET11/.NET10, including delayed writer continuation,
alias funding and budget reuse. Independent CPython3.13.2 golden agrees.
Do not move writer calls into the encoder's catch-and-Finish region. Native writes
must finish using the working buffer before the advance guard releases/reuse
begins; async callbacks inside encoding still hold the same admitted owner.

Selected next prototype direction: factor the existing file staging algorithm to
accept an admitted read-only UTF8 window; normal Python writes retain their input.
Native CSV can stage its builder before materialization. Native JSON can deliver
its private current buffer to the known native sink while preserving the same
advance machine and public copied-chunk path. Keep consumer errors outside encoder
failure cleanup. No new public representation, sink registry, memory scope layer,
per-object ledger, generic producer framework or codec shortcut.

Before code, settle the exact guard/borrow/error boundary. Stop and document a
conservative copy boundary if sharing would require parallel encoders, duplicated
codec loops or a new ownership graph; don't silently weaken alias/error behavior.
Bounded refinement/corrected observation is allowed. This is a design gate, not
a promised gain or permanent rejection if initial measurement is inconclusive.

Acceptance: public/custom chunk independence; callback reentry and genuine async
suspension; resumable aliases after ordinary writer errors; exact codec object/
positions and text count; encodings/newlines/BOM/partial prefixes; cancellation,
first terminal refusal, admitted capacity/replacement overlap and awaited cleanup.
Use focused existing JSON/CSV/file codec and ownership tests. Measure frozen whole
config and CSV rewrite plus unchanged text/report controls, and allocations/peaks;
disclose RAM versus VM filesystem condition. Retain all adverse outcomes and remove
displaced materialization only where the native lifetime proof applies. Full
platform/stress/package checks batch at the retained delivery checkpoint.
