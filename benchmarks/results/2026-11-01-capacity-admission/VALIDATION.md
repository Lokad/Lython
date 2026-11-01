# B27 delivery validation

- Pinned Release solution build: zero warnings/errors.
- Focused net11: 938 native and 2,748 public cases pass.
- Focused actual net10: 938 native and 2,748 public cases pass.
- Complete fixed comparison and non-timing ownership checks pass; see README.
- Final ordinary net11: **3,944 native / 15,324 public**, all pass.
- Explicit Release pack and installed isolated consumers at **830c6251** pass on
  actual .NET 11 RC3 / 10.0.8; empty core runtime dependency groups, symbols, guide
  assets and mediated file behavior pass. Published 0.10 artifacts/tag unchanged.
- Checkpoint **41264726223** fails original net11 stress on both platforms at
  the same 2,000-construction JSON subclass fixture: 405/406 public and all three
  native stress cases pass. Ordinary both-framework suites and installed package
  consumers pass on both platforms. Never requery this terminal run.
- The fixture now supplies 6 instead of 4 MiB for retained attribute capacity,
  preserving all 2,000 constructions, both APIs and zero-balance cleanup, and adds
  an explicit peak bound. A matching-loop pure probe under an 8 MiB diagnostic
  allowance reports 4,576,527 bytes peak; it prepends count rather than supplying
  the test's Globals, so this is supporting diagnosis, not the exact fixture peak.
- All 14 subclass-family cases, including original stress, pass per actual
  framework with the corrected 6 MiB fixture. Corrected **7d0157e1** checkpoint
  **41266997253** is terminal green: Windows/Ubuntu, ordinary both frameworks,
  original stress both frameworks and installed Release consumers. No scale or
  timeout was reduced. Never requery this terminal run.
- Subsequent cancellation fix **ae22392d** also passes full checkpoint
  **41269118889** on both platforms/frameworks, original stress and package
  consumers; focused 36 native / 169 public per framework and installed actual
  .NET 11 RC3 / 10.0.8 consumers pass locally. No publication/tag authorized.

The earlier B25/B26 checkpoint 41262561230 is terminal green on both platforms,
both target frameworks, original stress and installed consumers. It establishes
that earlier source's status, not B27 qualification; do not requery it.
