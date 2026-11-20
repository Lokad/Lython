# Utf8Regex development package boundary

The immutable 0.5.0-dev.111 package manifest records producer revision
`84c951f7f` and a clean Release build. The probe verified the Core and PythonRe
package and assembly hashes. On Windows .NET 10 and .NET 11, the package
copied the UTF-8 group name `é` and matched an ordinary anchored window.
Both targets rejected the reversed anchored window at byte start 3 and
byte end 1. CPython 3.13 returns an empty match and an empty participating
capture at scalar position 2 for that original subject.

The producer revision also uses `Unsafe.SizeOf` in Core construction
estimates. Both requirements remain open for final adapter qualification.
Lython's independently pinned optional adapter stays at 0.4.0.

The producer reports 405 passing Lython tests on each Windows target
against the exact local feed, using its existing development adapter and
host revision `98b9f87f7`. This reproduction checks the direct package
boundary. It leaves Linux and current-source adapter qualification open.

[Package manifest](producer-candidate.json) · [Source and verification output](checks.tar.xz).
