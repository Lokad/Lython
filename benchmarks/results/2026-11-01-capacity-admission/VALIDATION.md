# B27 delivery validation

- Pinned Release solution build: zero warnings/errors.
- Focused net11: 938 native and 2,748 public cases pass.
- Focused actual net10: 938 native and 2,748 public cases pass.
- Complete fixed comparison and non-timing ownership checks pass; see README.
- Final ordinary net11: **3,944 native / 15,324 public**, all pass.
- Explicit Release pack and installed isolated consumers at **830c6251** pass on
  actual .NET 11 RC3 / 10.0.8; empty core runtime dependency groups, symbols, guide
  assets and mediated file behavior pass. Published 0.10 artifacts/tag unchanged.
- Windows/Ubuntu both-framework/original-stress checkpoint **41264726223**:
  queued/running; final result pending. No publication/tag authorized.

The earlier B25/B26 checkpoint 41262561230 is terminal green on both platforms,
both target frameworks, original stress and installed consumers. It establishes
that earlier source's status, not B27 qualification; do not requery it.
