# Regex cursor creation-token deletion check

R454,2026-11-16UTC. Retain the detached creation-token bridge. One existing public
behavior/cleanup test is sufficient to reject its proposed deletion with the
current producer contract;no VM timing or unchanged broad qualification follows.

The R443immutable dev105 native adapter already forwards each pull token through
MoveNext(out range,token). Its creation-only CTS/registration detaches the earlier
Find token after preparation. The proposed deletion passes that token directly to
CreateMatchCursor and removes the CTS,forwarding registration and disposal.

Matched reference/deletion .NET11consumer images use identical current Lython
production source be176ed0 and immutable R443producer assemblies. Reference passes
CursorUsesCurrentAdvanceTokenAndReturnsItsWorkingAllowance:cancel creation's token,
advance4,096times with a current uncancelled token,then return allowance to baseline.
Deletion fails the first later pull with OperationCanceledException. Only adapter
source/reference compilation differs. Exact source,image hashes,TRX and stacks are
retained;no normal pin/core change,no new test,timeout or checkpoint counter.

The producer intentionally retains its cursor creation token. Per-pull tokens do
not replace it. Removing this bridge needs a creation/preparation interruption
contract independent of retained advancement authority. Do not trade away current-
operation cancellation or pass an uncancellable token through actual preparation.
This is a precise boundary,not a permanent ban on simplification after API changes.

Sibling source6ad34cab removed remaining UTF16-start entry points,but available
0.5.0-dev.105packages are byte-identical to R443 and are not that source snapshot.
They remain development inputs only. Caller-owned capture slots in sibling core
are not a PythonRe caller-buffer API:PythonRe still publishes snapshot arrays.
UTF8group-name enumeration and separate per-match result staging remain absent.
No publication,normal-pin promotion or producer unsafe-policy certification.

EffectiveOptions also cannot by itself replace the adapter's validated-prefix
flag bridge:parsing Verbose and reported flag bits are deliberately independent.
Existing public ParsingVerboseAndReportedFlagsRemainIndependent cases include
(32,true,plain)->32 and(32,true,inline-x)->96,which require distinguishing global
inline contributions from initial parsing options. This source/API assessment is
separate from the executed creation-token failure;no unexecuted timing claim.

Producer assemblies are preserved in the [R443archive](../2026-11-16-regex-dev105/procedure-and-images.tar.xz)
with the same hashes. Core remains decoupled;independent runtime work continues.
[Exact identities](identity.json),[observed test outcomes](checks.json),
[sources/binaries/builds/TRX](sources-and-checks.tar.xz).
