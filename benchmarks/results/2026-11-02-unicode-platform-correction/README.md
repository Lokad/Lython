# Platform Unicode compatibility correction

The first delivery atf9ae2d65 fails Ubuntu's unchanged full sigma golden at U+1C89. Correctionc23d1a10 covers Unicode16 pairs using existing compatibility data. The next delivery at082179c4 exposes U+A7D2, assigned later. Both failures are preserved; the first correction was incomplete, not a test timeout or optimizer failure.

Refresh the existing table from47 to87 intervals, covering printable characters assigned after Python3.13/Unicode15.1 through pinned Unicode17.0.0. The same table keeps simple mappings unchanged when either partner was unassigned in Python3.13, including older assigned characters. No per-character database, native dependency, policy, new lookup mechanism or runtime allocation is added; table growth is320 bytes of scalar endpoints. Unicode license is included in root third-party notices.

[Source URL/hash and all110 new pair partners](source.json). New partner vector follows independent CPython3.13.2 lower/upper goldens. Existing all-scalar printable SHA256 and complete sigma property-boundary/gap stdout golden stay unchanged. Fresh nonincremental Release solution build,216 internal/73 public focused cases pass on each actual.NET11RC3/.NET10.0.8, including retained-result funding/cancellation and helper/CSV controls. [TRX/build](validation-logs.tar.gz), [validation](validation.json). Corrected full platform/stress/package delivery passes at8f07764a; terminal proof is below. No measured speed claim; current performance evidence is not relabeled as this correction.

Full coherent delivery **8f07764a is qualified**: [terminal CI proof](delivery-ci.json),
Windows/Ubuntu ordinary.NET11/10, original Stress both and installed Release
consumers both all pass. Basef56338f2 was [superseded while incomplete](delivery-superseded.json)
to batch the retained casing change; its earlier successful scopes and both prior
actual failures remain visible. No repeated local matrix, publication or tag.
