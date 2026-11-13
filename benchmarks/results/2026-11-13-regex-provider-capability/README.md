# Optional regex adapter capability check

2026-11-13 local date (2026-11-12 UTC). Lython development continues independently
of final adapter qualification. Normal adapter/package pins remain0.4; core has
no Utf8Regex dependency. This is a new narrow capability check, not replayed
semantic/platform qualification or a performance claim.

Immutable0.5.0-dev.78 Core/PythonRe packages are available locally; exact hashes
are in [observations](observations.json). Public source freeze faa48f57, inspected
at metadata revision5ea22bbd. The three final-adapter gaps remain: separate
one-match working/result owners; native UTF8 group-name enumeration; and cursor
creation with cancellable preparation without retaining the creation caller's
token. Existing per-advance cancellation does not establish that last capability.

The new constructor/Compile subject-validation setting can express Lython's
valid, immutable borrowed UTF8 contract without repeating backend validity scans.
It does not waive pattern validation or required scalar/anchor/context facts.
However, the funded Compile overload drops inputValidation when forwarding to
its implementation. A bounded probe against actual immutable net11/net10 package
bytes confirms: unfunded AssumeValid reports AssumeValid; funded AssumeValid
reports Validate. Both runs retain45664B during construction and return to0 after
disposal. Actual runtimes:.NET11RC3 and.NET10.0.8, pinned Lython SDK. No timing or
equivalence conclusion follows from this property observation.

Before Lython's optional adapter selects this optimization, require the funded
overload to preserve the supplied setting, or explicitly retain conservative
adapter-local construction funding with the working public overload for a pinned
development bridge. Do not weaken ownership, hide an allowance or add backend
types to core. Keep conversion/metadata bridges outside core; qualify current-token
interruption and retained aliases separately from allocation/performance. Existing
0.4 behavior and Lython-owned contract tests remain independent and unchanged.

[Exact probe source/build/raw output](probe-source-checks.tar.xz).

## Bounded development bridge on current Lython

The existing dev73 adapter-local ownership/current-token bridge is rebuilt against
the immutable dev78 packages and current2d91eb1a core. It selects AssumeValid
through the working unfunded Compile overload while preserving the existing
conservative local compilation admission; it closes the current backend handle
after all users finish. Native cursor scratch still uses the existing shared
working allowance, pre-funded capture metadata and bounded live high water.
No new core API, producer edits or normal dependency/source changes were made.

Focused development checks pass **88 internal /534 public per actual target**
(.NET11RC3/.NET10), including Python regex/fnmatch/glob semantics, independent
capture/windows goldens, current tokens, native refusal, bounded16,384-match
enumeration and budget cleanup. Exact loaded Core/PythonRe hashes match the
supplied package bytes.29 normal files,570 production-source hashes and normal
161cd935 core binary remain unchanged. This is development qualification; it
does not establish backend speed, final adapter scope precision or new public
capabilities. The three precise native API gaps above remain. Normal adapter
pins stay0.4 and the adapter remains nonpackable.

[Qualification/isolation](dev78-bridge/qualification.json),
[reusable bridge/check sources and raw logs](dev78-bridge/source-checks.tar.xz).
