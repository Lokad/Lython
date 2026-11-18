# Native capture names: development adapter qualification

R528,2026-11-18UTC. Immutable producer source
**7be8240f31901e9fb96c278759b96d2ee4267df9**; Lython source925397f65.
Committed blobs were copied without touching the sibling's unrelated uncommitted
work. The ordinary nonpackable adapter remains on0.4.0; core dependencies and
publication are unchanged. This is development qualification, not an immutable
released-package, backend performance or safety-policy certification.

Producer TryCopyGroupNameUtf8 exposes each Python slot's name as UTF-8; unnamed
slots use ASCII decimal names. The candidate cursor adapter removes GetGroupNames,
integer parsing and Encoding.UTF8.GetBytes from named metadata construction.
It copies small names through16-byte scratch, skips unnamed slots, and admits
longer names into their final arrays under the existing coarse pattern charge.
The scratch is not a size ceiling. Native current-token calls handle actual copy
work; cached patterns retain no earlier caller token. Pattern/list/name capacity
and replacement/construction overlap remain covered by existing ownership.

Unpublished backend handles are explicitly disposed before the pattern allowance
is refunded on constructor/cancellation failure. Successful patterns keep their
backend and metadata owner until all users/borrows end. Existing original-subject
windows, byte/scalar offsets, flags, captures, caches and cursor cleanup remain.
No new owner/budget framework, quotas, tracing, fallback dialect or background work.

Both actual.NET11/10 pass **363regex checks each**, including real in-flight
cancellation, refused admission, retained results and budget reuse. Parent and
controlled subprocess images use the same recorded core/adapter/producer hashes.
Four independent copied-probe runs (both APIs/both targets) also match local
**CPython3.13.2** for short/unnamed/long ASCII/long Unicode names, retained matches
after purge, group indexes/expansion and nested capture-closure order. These are
semantic probes, distinct from the quiet-VM CPython3.13.16 performance baseline.
No CPU or allocation reduction is claimed from simultaneous producer changes.

The source still contains Unsafe.SizeOf APIs. AllowUnsafeBlocks=false merely
rejects unsafe C# syntax; it does not establish dependency-policy compliance.
Only the optional development adapter uses these producer images. Core releases
remain independent. Remaining producer simplification opportunities: creation/
preparation cancellation detached from later retained pulls, and separately
admitted result staging. Existing token/coarse-funding bridges remain local.
Only affected final adapter qualification against an inspected immutable released
package remains outstanding; core implementation and performance work continue.

[Source facts](source-facts.json),[producer source](producer-source.tar.xz),
[qualification](qualification.json),[checks](checks.json),
[parent/child identities](image-identity.json),[actual libraries](qualified-libraries.tar.xz),
[probes](name-probes.json),[probe source](names.py),
[adapter/procedures/checks](procedures-and-checks.tar.xz).
