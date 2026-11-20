# Utf8Regex dev.114 package boundary

The immutable local 0.5.0-dev.114 preview is from producer revision
`3b1c84467`. Direct Windows probes verified the Core and PythonRe
package and assembly hashes for .NET 10 and .NET 11. Both copied the
UTF-8 name `é` and passed an ordinary anchored match. Both rejected
the reversed byte window where CPython 3.13 returns an empty match and
an empty participating capture. The producer source still uses
`Unsafe.SizeOf<T>` in Core construction estimates.

The producer's Windows replay reports 405 successful Lython checks per
framework against host revision `98b9f87f7` and its development adapter.
The Linux replay is pending in the captured producer status. The normal
local adapter pin remains 0.4.0. Final package qualification still
requires correct original-subject windows and a fully managed producer.

[Candidate manifest](producer-candidate.json) · [Direct checks](checks.tar.xz).
