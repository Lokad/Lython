# Utf8Regex dev.115 package boundary

The unpublished preview comes from producer revision `050c51480`.
Direct Windows probes on .NET 10 and .NET 11 verified the immutable
Core and PythonRe package and assembly hashes. Both probes copied the
UTF-8 group name `é` and matched it in an ordinary anchored window.
Both rejected a reversed anchored start for which CPython 3.13 returns
an empty match and an empty participating capture. Core construction
estimates at the frozen producer revision still use `Unsafe.SizeOf<T>`.

The producer's detached replay reports 405 passing Lython checks
on Windows and Linux for each target. Its receipt matches the package
and assembly hashes from the candidate manifest. The replay uses host
revision `98b9f87f7` and the development adapter. The local adapter
continues to use released 0.4.0. Qualification for adoption still
requires correct original-subject windows and fully managed producer
implementation.

[Candidate manifest](producer-candidate.json) ·
[Lython replay identity and results](downstream.json) ·
[Direct checks](checks.tar.xz).
