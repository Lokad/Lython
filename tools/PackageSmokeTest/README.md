# Package consumer gate

Run `./tools/PackageSmokeTest/verify-package.ps1` on Windows or Linux with
PowerShell and .NET 10. It explicitly packs Release, verifies metadata,
dependencies, documentation and portable symbols, and restores that exact
artifact into a fresh consumer outside the repository. The consumer exercises
Compile, Run and RunAsync through only the package reference. It checks core
syntax, stream/text helpers and representative compatibility fixes, then uses an isolated in-memory
host to load/dump JSON and verify exact file bytes, including a writer left open
at successful execution end. Paused reads and writes prove that RunAsync awaits
host acquisition and publication. Its isolated
cache and SHA512 comparison ensure the candidate package was consumed.

Versions in the library project identify the next unpublished release
candidate. Do not reuse a published NuGet version or publish Debug outputs.
Keep assembly/file versions and package notes aligned with the candidate,
document changes in CHANGELOG, then validate both OSes and configurations.
The script and CI gate prepare and verify packages; publishing is a separate
release action. Generated artifacts and the temporary consumer remain
available for inspection.
