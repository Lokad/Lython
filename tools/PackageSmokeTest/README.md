# Package consumer gate

Run `./tools/pack.ps1` to generate the Release `Lokad.Lython` package and symbols
directly in `artifacts/nuget/`. There are no extra staging folders,
release bundles or generated manifests.

Run `./tools/PackageSmokeTest/verify-package.ps1` on Windows or Linux with
PowerShell and .NET 10. It runs the same pack command, verifies metadata,
dependencies, documentation and portable symbols, and restores that exact
core artifact into a fresh consumer outside the repository. The consumer verifies
that no regex package edge or assembly is present and that missing regex
functionality fails explicitly. It exercises
Compile, Run and RunAsync through only the package reference. It checks core
syntax, stream/text helpers and representative compatibility fixes, then uses an isolated in-memory
host to load/dump JSON and verify exact file bytes and UTF-16 endian/BOM/flush/append behavior, including a writer left open
at successful execution end. Paused reads and writes prove that RunAsync awaits
host acquisition and publication. Its isolated
cache and SHA512 comparison ensure the candidate package was consumed.

The gzip gate exercises sequential supplied-stream ownership and lazy reading,
then pauses host reads inside compression-level conversion and host writes
during filename-owned flush. It checks framing and final decoded bytes with an
independent .NET decoder, including an unclosed wrapper finalized at successful
execution end. The native FutureWarning control verifies exact captured and
host stderr, same-location suppression, real asynchronous output suspension and
synchronous rejection before an asynchronous write begins.

Versions in the library project identify the next unpublished release
candidate. Do not reuse a published NuGet version or publish Debug outputs.
Keep assembly/file versions and package notes aligned with the candidate,
document changes in CHANGELOG, then validate both OSes and configurations.
The script and CI gate prepare and verify packages; publishing is a separate
release action. Generated artifacts and the temporary consumer remain
available for inspection.

The CSV ownership control runs eight bounded batches per execution API under a
4 MiB shared allowance, including a genuinely paused asynchronous acquisition. Each result
keeps its execution storage funded until disposal; disposing it returns the
shared allowance to the compiled-script baseline independently of CLR collection.
Disposing the compiled script then returns the allowance to zero.

The core package includes the complete Python license in THIRD_PARTY_NOTICES.md.
The former third_party/Python.LICENSE.txt entry is rejected. The optional
Utf8Regex adapter remains a non-packable repository project, exercised by the
public-API suite. That coverage does not clear separate backend qualification.

Packing derives README.md from the root guide and turns relative documentation
links into GitHub URLs for the package source commit. The gate verifies that
this changes only links, preserving code examples and all other guide content.
