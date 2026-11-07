# Native hash cancellation — R33 source gate

2026-11-07, committed source42643b1b, actual installed-consumer coreE4BC0473.
No production change. The small isolated friend-assembly probe constructs an
execution with a host token, creates three cold native strings, then cancels.
Synchronous PrepareKeyHash still returns a hash; async preparation refuses at its
entry check; the existing explicit-token string hash overload refuses correctly.
Final shared allowance is zero. Exact source/commands/output are retained.

This demonstrates missing token forwarding in the synchronous native-key route.
It does not demonstrate public-command in-flight interruption: public outer checks
can reject earlier, and the async probe exercises only its entry check. Source
shows both eventual native-string scans can reach GetPyHashCode/GetHashCode with
a default token. HashUtf8 has4096-byte chunks, but the active token is absent.
PyBytes hashing separately uses one whole HashCode.AddBytes call. Explicit
str.__hash__/bytes.__hash__ wrappers discard their current context too.

Next bounded correction: pass current authority through actual cold native hashing
(public hash, native dictionary/set keys, nested compound keys and explicit native
hash members), reusing the current string token overload and existing chunking.
Assess bytes in the same request path without opening a universal hashing rewrite.
Keep normalized hash/equality semantics, cached values/caller independence,
first memory refusal, protocol evaluation order, cycle/depth checks and context-free
CLR comparers. No new policy, counter, watchdog, owner, ambient async state or
background operation. Establish cancellation during real scanning, not just entry/
exit, and preserve cleanup/budget reuse. Use focused checks and one short affected
workload comparison; no predicted performance improvement.

B377 previously sampled structural hashing33/1,095leaves, but this correctness
finding does not establish a dominant removable CPU phase. The broader execution
redesign roadmap remains active independently. Full R30–R32 delivery is still
running and will not qualify an unimplemented R33 correction.
