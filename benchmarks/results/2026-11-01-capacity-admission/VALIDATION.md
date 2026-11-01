# B27 delivery validation

- Pinned Release solution build: zero warnings/errors.
- Focused net11: 938 native and 2,748 public cases pass.
- Focused actual net10: 938 native and 2,748 public cases pass.
- Complete fixed comparison and non-timing ownership checks pass; see README.
- Final ordinary net11: **3,944 native / 15,324 public**, all pass.
- Windows/Ubuntu both-framework/original-stress checkpoint and installed Release
  consumers: pending at this source checkpoint.

The earlier B25/B26 checkpoint 41262561230 is terminal green on both platforms,
both target frameworks, original stress and installed consumers. It establishes
that earlier source's status, not B27 qualification; do not requery it.
