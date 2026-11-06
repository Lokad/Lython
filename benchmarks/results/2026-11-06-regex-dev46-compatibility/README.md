# Optional regex provider — immutable dev46 compatibility

Lython source799895ea consumes the supplied immutable0.5.0-dev.46 Core/PythonRe
inputs from package source6957b35aa51ee5730252011085e21669767ee1f9 in an isolated
development build. Both package bytes and loaded net10/net11 DLLs are identified
in identity.json/qualification.json. Normal pin0.4.0, production provider/project
and central versions are hash-verified unchanged. The core stays dependency-free;
the optional adapter remains nonpackable. No producer source is changed.

The existing U04 development bridge is byte-identical to its qualified dev44
source. Documented member identities are unchanged for both packages/TFMs;
compilation and semantic checks establish actual consumption. There is no new
adapter plumbing justified by these inputs.

Both-target Release builds pass with0warnings/errors. Per actual.NET11/.NET10:
85internal and482public selected tests pass, including regex/fnmatch/glob semantics,
caller capture spans, UTF8/scalar windows, cancellation, terminal refusal, retained
groups/caches and the native cursor's bounded working admission/refund/reuse.
The private bridge still uses the supplied native high-water working allowance;
this is not cumulative allocation accounting or a new Lython memory subsystem.
Original fixture/source identities and isolated restore lockfiles are retained.

This is development compatibility qualification, not a new Lython performance
comparison, package publication or final production-adapter safety qualification.
Producer timings are not transferred to Lython. Final released-package promotion
remains separate. One-shot DetailedData still lacks the working-owner parameter
used by preparation; native name lookup does not enumerate group names; existing
creation-token authority remains a contract limitation to resolve separately.
Keep conservative preflight and adapter-local conversion where still needed;
these limits do not stop core development.

Initial preparation assumed the wrong nuspec XML namespace and stopped before
build/test or normal-input mutation. Namespace-independent metadata selection
corrects that input check; preserve the original condition in the history archive.
