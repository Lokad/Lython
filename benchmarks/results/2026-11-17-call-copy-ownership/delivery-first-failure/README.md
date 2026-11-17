# First copied-call ownership qualification: failed

Full CI44091209377 at f2cb4008 failed on Windows and Ubuntu. Each platform
caught both synchronous/asynchronous existing
`BoundMethodInvocationBoundaryTests.GeneralWrappedCallablesKeepFreshRetainableArrays`
regressions: the owned callable ABI allows retaining arrays, but the attempted
temporary-lifetime fix cleared them at return. Focused tests missed this contract.
The first attempt is not qualified. .NET10, stress and package steps were skipped.

Exact committed inputs, terminal status and downloaded logs are preserved here;
never requery this terminal run. The earlier B903 timing belongs only to that
incorrect frozen384327c2 source. Corrected ownership needs separate validation.
