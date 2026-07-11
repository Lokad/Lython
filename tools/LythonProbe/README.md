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

The probe host is deterministic and intentionally supplies no file, directory,
standard-stream, subprocess, or local-import capabilities. Use the xUnit test
harness for host-mediated scenarios. Normal Lython execution limits remain in
force. `--compare-python` executes the supplied source with the selected local
CPython executable in isolated UTF-8 mode, so use it only for trusted audit
snippets. Managed exceptions that escape `LythonEngine.Run` are reported with a
`CLR:` failure type so later batch cases can continue.
