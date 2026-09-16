# LythonProbe

`LythonProbe` is a small compatibility-audit CLI over Lython's public embedding
API. It makes independent semantic probes easy to run without adding temporary
xUnit cases or creating a custom host. The launcher builds the tool on first use
and rebuilds it when its source changes.

Run a command-line snippet and compare it with the local CPython installation:

```powershell
./tools/LythonProbe/lythonprobe.ps1 -c 'import math; print(math.sqrt(9))' --compare-python
```

Run a source file or pipe plain source on stdin:

```powershell
./tools/LythonProbe/lythonprobe.ps1 probe.py --json
Get-Content -Raw probe.py | ./tools/LythonProbe/lythonprobe.ps1
```

For an audit matrix, pass a JSON array and receive one JSON object per line:

```powershell
$snippets = @(
  "import itertools`nprint(list(itertools.islice('abc', None)))",
  "import operator`nprint(operator.length_hint(iter([1, 2])))"
)
ConvertTo-Json -InputObject $snippets -Compress |
  ./tools/LythonProbe/lythonprobe.ps1 --batch-json --compare-python
```

Select the execution path and cap accounted memory per probe (JSON reports carry the effective options, accounted peaks, denied bytes, and failure spans/frames):

```powershell
./tools/LythonProbe/lythonprobe.ps1 -c 'x = [0]*2000000' --max-memory-bytes 100000 --json
./tools/LythonProbe/lythonprobe.ps1 -c 'print(40 + 2)' --async
```

Exit codes: `0` when every snippet succeeds (and matches CPython when compared), `1` when a snippet fails, mismatches, or exceeds its budget, and `2` for usage, input, batch, or tool errors.

The probe host is deterministic and intentionally supplies no file, directory,
standard-stream, subprocess, or local-import capabilities. Use the xUnit test
harness for host-mediated scenarios. Normal Lython execution limits remain in
force. `--compare-python` executes the supplied source with the selected local
CPython executable in isolated UTF-8 mode, so use it only for trusted audit
snippets. Managed exceptions that escape `LythonEngine.Run` are reported with a
`CLR:` failure type so later batch cases can continue.
