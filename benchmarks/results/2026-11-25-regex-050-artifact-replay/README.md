# Optional regex adapter package replay

The replay uses Lython `c1984f96b` and the private funded bridge with its
construction refund. The local 0.5.0 artifacts contain producer `dcfe880ab`.
Their exact package and assembly hashes differ from the earlier development
input. Locked restores succeed. The adapter build fails on Windows .NET 11
and Ubuntu .NET 10. These artifacts lack `Utf8RegexPolicy`, UTF-8 group-name
copying, `Utf8PythonMatchMode` and the cursor's explicit end bound.

A second replay packs producer `6edc01772` in a private feed as
`0.5.0-dev.lython.6edc01772`, using its pinned .NET 11.0.100 SDK. This producer
supports the existing bridge. The first build passes 513 of 514 regex tests
on each platform. The failing lifetime test expected spare working capacity
after a tiny successful pull. The refinement checks that the compiled owner
stays funded and that disposing the cursor and subject restores the balance
and releases the borrowed input. The separate preflight, refusal and real
in-flight cancellation tests remain in the suite.

Windows .NET 11 and Ubuntu .NET 10 then pass all 514 selected Release checks
each. Producer binaries in both the test assembly directory and the
cancellation subprocess directory match the exact package entries. The
platform builds of Lython core have no Utf8Regex dependency. The committed
adapter pin stays at 0.4.0.

This replay qualifies development compatibility for the tested shapes.
Reversed anchored windows and the staged parser/compiler callbacks still
need producer support. Reported flags and compiled-program equality still
need full backend coverage. The replay supplies no timing for this producer.
Published-package qualification is separate.

[Package and assembly identities](verification.json) ·
[Patches, build and test logs](procedures.tar.xz).
