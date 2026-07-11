# Lython supported-module coverage gap backlog

This is an audit backlog, not a proposal to widen Lython into unrestricted Python. It records behavior inside the module subsets advertised by `README.md` and `SPEC.md` that does not currently follow the promised Python shape. Deliberate boundaries—host-mediated authority, deterministic random generation, fixed-precision `Decimal`, double-coerced statistical aggregates, eager operations explicitly permitted by the spec, and APIs that are explicitly unsupported—are excluded.

## Audit reference

- Repository state reviewed on 2026-07-11, after completion of the previous language/runtime coverage backlog.
- Baseline: `dotnet test Lokad.Lython.slnx --no-restore --nologo` passes all 1,712 tests.
- Ground truth for standard-library behavior is the local CPython 3.12.1 installation. The retained behaviors are stable for Lython's declared Python 3.13 compatibility family unless a ticket explicitly describes an internal Lython-version inconsistency.
- The local CPython environment contains `openpyxl` 3.1.4; workbook and style tickets were executed against that installation as well as the Lython implementation.
- Pure reproductions were rerun through `tools/LythonProbe`, whose comparison mode executes the same trusted source under Lython and isolated UTF-8 CPython; host-mediated cases continued to use the xUnit harness.
- Every reproducer was run independently through the current `LythonEngine`. A failure reported below is therefore present after, rather than inherited from, the completed language/runtime backlog.
- The former LY-MOD-013 was withdrawn: `SPEC.md` explicitly permits difflib's generator APIs to return materialized lists, so iterator identity is not an in-scope gap. Its identifier is intentionally not reused.
- `P0` means a script can escape Lython's failure projection and terminate or throw through the embedding process. `P1` means silent wrong behavior or observably wrong public state. `P2` means a valid supported Python shape is rejected, or a narrower edge contract differs without corrupting a value.

## Review coverage

The audit traced every allowlisted module from import registration through its runtime implementation, static contracts, module tests, and stated exclusions. The result below distinguishes retained tickets from modules for which no additional in-scope reproducer survived validation.

| Module | Audit result |
| --- | --- |
| `argparse` | LY-MOD-001, LY-MOD-024 |
| `collections`, `collections.abc` | LY-MOD-002, LY-MOD-003, LY-MOD-025, LY-MOD-026; inert `abc` names are an explicit boundary |
| `copy` | LY-MOD-004 |
| `csv` | LY-MOD-005; the Lython-defined `\n` dialect default and string-only reader rows are excluded |
| `dataclasses` | LY-MOD-006, LY-MOD-007, LY-MOD-027, LY-MOD-028 |
| `datetime` | LY-MOD-008 through LY-MOD-010, LY-MOD-029, LY-MOD-030 |
| `decimal` | LY-MOD-011, LY-MOD-012; arbitrary precision and non-finite values are excluded |
| `difflib` | No retained gap; materialized generator results are explicitly permitted, and content/record/protocol probes otherwise matched |
| `fnmatch` | No retained gap; deterministic case sensitivity and translation spelling are explicit |
| `functools` | LY-MOD-031, LY-MOD-032; annotation-only dispatch and placeholders are explicit exclusions |
| `glob` | No retained gap; host mediation and permitted eager materialization were respected |
| `itertools` | LY-MOD-014 |
| `json` | LY-MOD-015, LY-MOD-016, LY-MOD-033, LY-MOD-034 |
| `math` | LY-MOD-035 |
| `operator` | LY-MOD-017, LY-MOD-036 |
| `openpyxl` and its allowlisted submodules | LY-MOD-023, LY-MOD-043 through LY-MOD-047 |
| `os`, `os.path` | No retained gap after applying the normalized contained-path and environment model |
| `pathlib` | LY-MOD-037 after applying the shared POSIX-like model and explicit eager/unsupported boundaries |
| `pkgutil` | No retained gap; materialized discovery and rejected ambient importer/resource helpers are explicit |
| `random` | LY-MOD-018; algorithm/output differences caused by deterministic design are excluded |
| `re` | LY-MOD-019, LY-MOD-038; bytes, locale, debug, and syntax outside the specified regex profile are excluded |
| `shutil` | No retained gap within the host-mediated text/file copy and move surface |
| `statistics` | LY-MOD-020, LY-MOD-039 through LY-MOD-041; documented double coercion and deterministic sampling are excluded |
| `subprocess` | No retained gap within the optional host capability and captured text-only process surface |
| `sys` | LY-MOD-021, LY-MOD-042; contained metadata values are otherwise allowed to describe Lython rather than CPython |
| `typing` | LY-MOD-022; runtime enforcement and protocol checks are explicitly outside scope |

## Tickets

### LY-MOD-001 — Honor `--` as argparse's end-of-options marker (`P2`)

```python
import argparse

p = argparse.ArgumentParser(add_help=False)
p.add_argument("value")
print(p.parse_args(["--", "-x"]).value)
```

**CPython:** prints `-x`. **Lython:** raises `SystemExit: unrecognized arguments: -x`. The ordinary parser surface includes short options and positional arguments; the standard `--` marker must stop option recognition and must not itself become a positional value.

### LY-MOD-002 — Let `Counter` retain non-integral numeric counts (`P2`)

```python
from collections import Counter

c = Counter(a=1.5, b=2)
print(c.total())
```

**CPython:** prints `3.5`. **Lython:** raises `TypeError: Counter mapping values must be integers`. Python's `Counter` permits numeric counts beyond integers, and `total()`, ordering, updates, and arithmetic should operate on those values where Lython already supports their numeric type.

### LY-MOD-003 — Reduce large `deque.rotate` counts before narrowing (`P2`)

```python
from collections import deque

d = deque([1, 2, 3])
d.rotate(1_000_000_000_000)
print(list(d))
```

**CPython:** prints `[3, 1, 2]`. **Lython:** raises `OverflowError: deque rotation is too large`. The count is a valid Python integer and can be reduced modulo the deque length before any machine-integer conversion.

### LY-MOD-004 — Preserve an all-atomic tuple during `deepcopy` (`P2`)

```python
import copy

x = (1, 2)
print(copy.deepcopy(x) is x)
```

**CPython:** `True`. **Lython:** `False`. Python reuses the original tuple when recursively copying it changes no element identity. Lython always constructs a replacement tuple, making an otherwise immutable atomic value observably different.

### LY-MOD-005 — Quote a single empty CSV field (`P1`)

```python
import csv

with open("empty.csv", "w", newline="") as f:
    print(csv.writer(f, lineterminator="\n").writerow([""]))
```

**CPython:** writes `""\n` and prints `3`. **Lython:** writes only `\n` and prints `1`. A bare line terminator represents an empty row, not a row containing one empty field, so the current output loses record shape on a supported scalar value.

### LY-MOD-006 — Use field-value `repr` in generated dataclass representations (`P1`)

```python
from dataclasses import dataclass

@dataclass
class Box:
    name: str

print(Box("x"))
```

**CPython:** `Box(name='x')`. **Lython:** `Box(name=x)`. Generated dataclass `repr` must use each field value's representation, not its display string; the current result is ambiguous and is not valid constructor-shaped text.

### LY-MOD-007 — Preserve non-string mapping keys in `dataclasses.asdict` (`P2`)

```python
from dataclasses import asdict, dataclass

@dataclass
class Box:
    data: dict

print(asdict(Box({1: "x"})))
```

**CPython:** prints `{'data': {1: 'x'}}`. **Lython:** raises `TypeError` saying that `asdict()` only supports dictionaries with string keys inside dataclass values. The advertised recursive conversion helper must retain supported hashable key types rather than impose a JSON-object restriction.

### LY-MOD-008 — Implement `timedelta.__str__` separately from `repr` (`P1`)

```python
import datetime

d = datetime.timedelta(days=1, seconds=2, microseconds=3)
print(str(d))
```

**CPython:** `1 day, 0:00:02.000003`. **Lython:** `datetime.timedelta(days=1, seconds=2, microseconds=3)`. The datetime subset promises CPython-shaped formatting, but Lython routes both display and representation through the constructor-style representation.

### LY-MOD-009 — Include `tzinfo` and `fold` in aware time/datetime representations (`P1`)

```python
import datetime

print(repr(datetime.time(1, 2, tzinfo=datetime.timezone.utc, fold=1)))
print(repr(datetime.datetime(2024, 1, 2, tzinfo=datetime.timezone.utc, fold=1)))
```

**CPython:** both representations include `tzinfo=datetime.timezone.utc` and `fold=1`. **Lython:** prints `datetime.time(1, 2, 0, 0)` and `datetime.datetime(2024, 1, 2, 0, 0, 0, 0)`. The current representations silently describe different naive, non-folded objects.

### LY-MOD-010 — Accept fixed UTC offsets with sub-minute precision (`P2`)

```python
import datetime

z = datetime.timezone(datetime.timedelta(seconds=30))
print(z.utcoffset(None))
print(datetime.time(1, tzinfo=z).isoformat())
```

**CPython:** prints `0:00:30` and `01:00:00+00:00:30`. **Lython:** rejects construction because the offset is not a whole number of minutes. Fixed-offset timezones are advertised without that obsolete restriction, and these values fit Lython's supported timedelta range.

### LY-MOD-011 — Apply the active decimal context's rounding mode (`P1`)

```python
from decimal import Decimal, ROUND_DOWN, getcontext

context = getcontext()
context.rounding = ROUND_DOWN
print(round(Decimal("1.29"), 1))
```

**CPython:** `1.2`. **Lython:** `1.3`. Although context and rounding constants are exposed, `round` ignores the active context and uses a different rounding rule. The fix should cover the other advertised integral/quantizing operations that consume context rounding as well.

### LY-MOD-012 — Retain a decimal's exponent and quantum metadata (`P1`)

```python
from decimal import Decimal

x = Decimal("1E+3")
print(str(x))
print(x.as_tuple())
```

**CPython:** prints `1E+3` and `DecimalTuple(sign=0, digits=(1,), exponent=3)`. **Lython:** prints `1000` and `DecimalTuple(sign=0, digits=(1, 0, 0, 0), exponent=0)`. This is not a request for arbitrary precision: the value fits .NET `decimal`, but the advertised string/tuple conversion surface discards its representational exponent.

### LY-MOD-014 — Support open-ended `itertools.islice` stops (`P2`)

```python
import itertools

print(list(itertools.islice("abc", None)))
```

**CPython:** prints `['a', 'b', 'c']`. **Lython:** raises `TypeError: itertools.islice() stop must be a non-negative integer`. `None` is the standard spelling for an unbounded stop in both the two-argument and explicit-start forms.

### LY-MOD-015 — Preserve Python float spelling when converting JSON object keys (`P1`)

```python
import json

print(json.dumps({1.0: "x", -0.0: "z"}))
```

**CPython:** `{"1.0": "x", "-0.0": "z"}`. **Lython:** `{"1": "x", "-0": "z"}`. JSON values already use Python-compatible float rendering, but the dictionary-key path uses a separate conversion that loses `.0` and signed-zero spelling.

### LY-MOD-016 — Sort JSON dictionaries before key stringification (`P1`)

```python
import json

print(json.dumps({1: "a", "2": "b"}, sort_keys=True))
```

**CPython:** raises `TypeError` because `int` and `str` keys are not orderable with each other. **Lython:** silently emits `{"1": "a", "2": "b"}` by converting keys first. `sort_keys=True` must sort the original supported keys under Python comparison semantics, while ordinary key conversion remains a later serialization step.

### LY-MOD-017 — Honor the `__length_hint__` protocol in `operator.length_hint` (`P1`)

```python
import operator

class Sized:
    def __length_hint__(self):
        return 7

print(operator.length_hint(Sized()))
```

**CPython:** `7`. **Lython:** `0`. The specification says the operator helper reuses Lython's Python-shaped semantics, but the implementation checks concrete built-in containers and never dispatches the supported user-object protocol.

### LY-MOD-018 — Require sequences in `random.choice` and `random.sample` (`P1`)

```python
import random

print(random.choice({1}))
```

**CPython:** raises `TypeError` because a set is not subscriptable. **Lython:** prints `1` after accepting and materializing the set as a generic iterable. Deterministic output is an intentional Lython property; accepting a population shape that Python rejects is not. The shared validation should also cover `sample`, which currently accepts sets.

### LY-MOD-019 — Publish CPython-compatible regular-expression flag values (`P1`)

```python
import re

print(int(re.I), int(re.M), int(re.S), int(re.X), int(re.A), int(re.U))
print(re.compile("(?i)x").flags)
print(re.findall(r"\w+", "é_1", re.A))
```

**CPython:** prints `2 8 16 64 256 32`, then `34`, then `['_1']`. **Lython:** prints `1 4 8 16 32 32`, then `32`, then `['é_1']`; notably `ASCII` and `UNICODE` collide and the public `ASCII` flag does not enable ASCII character-class behavior. The module advertises the usual integer flags, so aliases, bitwise combinations, constructor flags, inline flags, `Pattern.flags`, and backend behavior must use the public CPython bit assignments even if the regex backend uses a different internal enum.

### LY-MOD-020 — Allow generic hashable data in `statistics.mode` and `multimode` (`P2`)

```python
import statistics

print(statistics.multimode("abac"))
print(statistics.mode("abac"))
```

**CPython:** prints `['a']` and `a`. **Lython:** rejects the first call because it expects real numbers. Unlike averages and variance, the advertised mode helpers operate on generic discrete/hashable observations and do not require numeric coercion.

### LY-MOD-021 — Derive `sys` version metadata from Lython's declared target (`P1`)

```python
import sys

print(sys.version)
print(sys.version_info[:2])
```

**Lython's public compatibility constant:** `LythonPythonVersion.VersionFamily == "Python 3.13"`. **Lython script output:** `3.11.0 (Lython)` and `(3, 11)`, with matching stale 3.11 values in `hexversion` and `sys.implementation.version`. All exposed version views should come from one source of truth and describe the declared Lython compatibility family.

### LY-MOD-022 — Normalize `typing` inspection origins and arguments (`P1`)

```python
from typing import Dict, List, Optional, get_args, get_origin

print(get_origin(List[int]) is list)
print(get_origin(Dict[str, int]) is dict)
print(get_origin(Optional[int]))
print(get_args(Optional[int]))
```

**CPython:** the first two values are `True`; the optional alias has `typing.Union` as its origin and arguments `(int, NoneType)`. **Lython:** the first two values are `False`, returns the inert `typing.Optional` alias as the optional origin, and reports only `(int,)`. Typing remains non-enforcing, but its advertised inspection helpers must normalize builtin collection aliases and `Optional`/`Union` composition to their Python-shaped origins and arguments.

### LY-MOD-023 — Keep `Workbook.named_styles` public and `_named_styles` internal (`P1`)

```python
from openpyxl import Workbook

print(Workbook().named_styles == ["Normal"])
```

**Upstream openpyxl 3.1.4:** `True`; [`Workbook.named_styles`](https://openpyxl.readthedocs.io/en/latest/_modules/openpyxl/workbook/workbook.html) returns `self._named_styles.names`. **Lython:** `False` and renders `[Normal]` because the public property exposes its internal style objects. The vanilla-Python-shaped workbook subset should return style-name strings publicly and reserve mutable style records for the private collection.

### LY-MOD-024 — Treat negative numeric tokens as positional argparse values (`P2`)

```python
import argparse

p = argparse.ArgumentParser(add_help=False)
p.add_argument("value", type=int)
print(p.parse_args(["-2"]).value)
```

**CPython:** prints `-2`. **Lython:** exits with status 2 and reports `unrecognized arguments: -2`. Argparse's ordinary negative-number disambiguation treats a token as a positional value when it does not match a registered negative-number option; Lython currently classifies every leading-hyphen token as an option.

### LY-MOD-025 — Implement the advertised unary `Counter` operations (`P2`)

```python
from collections import Counter

c = Counter(a=2, b=-1, c=0)
print(+c)
print(-c)
```

**CPython:** prints `Counter({'a': 2})` and `Counter({'b': 1})`. **Lython:** rejects both unary expressions with static `Operand is not numeric` diagnostics. `SPEC.md` explicitly includes unary `+` and `-` in the supported `Counter` surface; they must drop non-positive results and preserve insertion order like the already advertised positive-count arithmetic.

### LY-MOD-026 — Give `ChainMap` its mapping length (`P2`)

```python
from collections import ChainMap

c = ChainMap({"a": 1}, {"a": 2, "b": 3})
print(len(c))
```

**CPython:** prints `2`, counting distinct visible keys. **Lython:** raises `TypeError: Object has no len()`. The advertised layered mapping already supports iteration, `maps`, `parents`, and `new_child`; its fundamental mapping length must use the same union-of-keys view.

### LY-MOD-027 — Keep `dataclasses.replace` failures catchable as `ValueError` (`P2`)

```python
from dataclasses import dataclass, field, replace

@dataclass
class Box:
    value: int
    cached: int = field(init=False, default=0)

try:
    replace(Box(1), cached=2)
except ValueError:
    print("caught")
```

**CPython:** prints `caught`. **Lython:** rejects the script statically with a `dataclasses.replace() cannot override init=False field` diagnostic, so the Python exception cannot be caught. The call is invalid, but CPython deliberately raises `ValueError` at runtime; the supported helper must preserve that failure phase and type.

### LY-MOD-028 — Reuse the class's public dataclass `Field` objects (`P1`)

```python
from dataclasses import dataclass, fields

@dataclass
class Box:
    value: int

print(fields(Box)[0] is fields(Box)[0])
```

**CPython:** `True`. **Lython:** `False`. Every `fields()` call constructs fresh compatibility records instead of returning the `Field` objects stored for the dataclass. That breaks stable field identity and also makes `fields(Box) == fields(Box())` false despite both queries describing the same class.

### LY-MOD-029 — Use a space in `datetime.__str__` (`P1`)

```python
import datetime

x = datetime.datetime(2024, 1, 2, 3, 4, 5)
print(str(x))
print(format(x, ""))
```

**CPython:** both lines are `2024-01-02 03:04:05`. **Lython:** `str(x)` uses `2024-01-02T03:04:05`, while empty-format rendering correctly uses the space. ISO formatting with its default `T` separator is not the same contract as `datetime.__str__`.

### LY-MOD-030 — Accept the supported ISO basic and week-date forms (`P2`)

```python
import datetime

print(datetime.date.fromisoformat("2024-W01-2"))
print(datetime.datetime.fromisoformat("20240102T030405"))
print(datetime.time.fromisoformat("T03:04:05"))
```

**CPython:** prints `2024-01-02`, `2024-01-02 03:04:05`, and `03:04:05`. **Lython:** each call, when run independently, raises `ValueError` from the corresponding .NET parser. ISO parsing is advertised; CPython accepts calendar basic forms, ISO week-date forms (with or without separators), compact times, and an optional leading `T`, all without requiring timezone-database or locale support.

### LY-MOD-031 — Make `singledispatch.dispatch` use the registered MRO (`P1`)

```python
from functools import singledispatch

@singledispatch
def f(value):
    return "base"

@f.register(int)
def _(value):
    return "int"

print(f.dispatch(bool)(True))
```

**CPython:** prints `int` because `bool` inherits from `int`. **Lython:** prints `base`. Ordinary invocation already selects the nearest registered MRO type; the public `dispatch(cls)` helper incorrectly performs an exact lookup and must share the same resolver.

### LY-MOD-032 — Give `cmp_to_key` wrappers their rich comparisons (`P2`)

```python
import functools

key = functools.cmp_to_key(lambda a, b: (a > b) - (a < b))
print(key(1) < key(2))
```

**CPython:** `True`. **Lython:** raises `TypeError: Values are not comparable`. Sorting happens to recognize Lython's wrapper internally, but the helper promises key-wrapper objects whose `<`, `<=`, `==`, `>`, and `>=` operations invoke the supplied comparator directly.

### LY-MOD-033 — Report exact `JSONDecodeError` positions (`P1`)

```python
import json

for text in ["1.", '"\\x"']:
    try:
        json.loads(text)
    except json.JSONDecodeError as error:
        print(error.pos, error.lineno, error.colno)
```

**CPython:** prints `1 1 2` twice. **Lython:** prints `2 1 3` for both inputs. The advertised error record points after the actual failure for incomplete fractional numbers and invalid string escapes; the same off-by-one path affects other lexer failures while array trailing-comma positions happen to match.

### LY-MOD-034 — Bound JSON recursion even when circular checks are disabled (`P0`)

```python
import json

value = []
value.append(value)
json.dumps(value, check_circular=False)
```

**CPython:** raises `RecursionError`. **Lython:** recursively enters `AppendJsonValue` until a CLR `StackOverflowException` terminates the process; it does not return a `LythonExecutionResult`. Disabling identity-based cycle detection must not disable contained recursion accounting or allow a script to escape the runtime's failure projection.

### LY-MOD-035 — Dispatch Python numeric-conversion protocols in `math` (`P2`)

```python
import math

class Rounded:
    def __ceil__(self): return 7
    def __floor__(self): return 6
    def __trunc__(self): return 5

class Indexed:
    def __index__(self): return 5

print(math.ceil(Rounded()), math.floor(Rounded()), math.trunc(Rounded()))
print(math.factorial(Indexed()), math.isqrt(Indexed()))
```

**CPython:** prints `7 6 5` and `120 2`. **Lython:** the calls, checked independently, reject the objects as non-real or non-integer values. The exposed helpers must honor `__ceil__`, `__floor__`, `__trunc__`, and `__index__`; the integer protocol also applies to `comb`, `perm`, `gcd`, and `lcm`.

### LY-MOD-036 — Dispatch `__index__` in `operator.index` (`P2`)

```python
import operator

class Indexed:
    def __index__(self):
        return 1

print(operator.index(Indexed()))
```

**CPython:** prints `1`. **Lython:** raises `TypeError: operator.index(obj) expects an integer-compatible value`. The direct-function equivalent is specifically the public entry point for the `__index__` protocol, not merely an integer type check.

### LY-MOD-037 — Construct the empty path as `.` without a CLR escape (`P0`)

```python
from pathlib import PurePosixPath

print(PurePosixPath(""))
```

**CPython:** prints `.`. **Lython:** `PathOps.Normalize` throws `System.ArgumentException` through `LythonEngine.Run`, bypassing the normal Python failure projection and aborting an in-process probe batch. `Path()`, `PurePath()`, and their POSIX aliases must treat both no components and an empty component as the current-directory path.

### LY-MOD-038 — Preserve adjacent non-empty regex matches after empty matches (`P1`)

```python
import re

print([(m.group(), m.span()) for m in re.finditer(".*?", "ab")])
print(re.sub(".*?", "-", "ab"))
```

**CPython:** reports empty and non-empty matches at each position and prints `-----`. **Lython:** reports only the three empty matches and prints `-a-b-`. Python permits a non-empty match to start immediately after an empty match at the same position; the current iterator advances the subject unconditionally and skips those valid matches. `findall`, `finditer`, `sub`, and `subn` share the defect.

### LY-MOD-039 — Support weighted `statistics.fmean` (`P2`)

```python
import statistics

print(statistics.fmean([1, 2, 3], weights=[1, 1, 2]))
```

**CPython:** prints `2.25`. **Lython:** rejects the keyword statically because `fmean` is registered with a one-argument signature. Weighted floating mean is part of the common average helper's Python signature and fits the documented double-coercion model.

### LY-MOD-040 — Accept supplied centers in variance helpers (`P2`)

```python
import statistics

print(statistics.pvariance([1, 2, 3], mu=2))
print(statistics.variance([1, 2, 3], xbar=2))
```

**CPython:** prints `0.6666666666666666` and `1`. **Lython:** rejects both keywords statically because every variance and standard-deviation helper is registered with a one-argument signature. The advertised variance surface should accept its standard optional precomputed center (`mu` for population helpers, `xbar` for sample helpers), including the corresponding `pstdev` and `stdev` paths.

### LY-MOD-041 — Add `NormalDist.zscore` (`P2`)

```python
import statistics

print(statistics.NormalDist(1, 2).zscore(5))
```

**CPython:** prints `2.0`. **Lython:** rejects the member statically because `NormalDist` has no `zscore`. This is a pure, deterministic method on the advertised distribution type and requires no expansion into KDEs, entropy, or additional numeric representations.

### LY-MOD-042 — Expose the named fields of `sys.version_info` (`P1`)

```python
import sys

print(sys.version_info.major, sys.version_info.minor)
print(sys.version_info.releaselevel, sys.version_info.serial)
```

**CPython:** prints its four named values. **Lython:** raises `AttributeError` on `major`; its compatibility value only supports tuple indexing. In addition to correcting the stale numbers under LY-MOD-021, the supported metadata record must expose the ordinary `major`, `minor`, `micro`, `releaselevel`, and `serial` fields.

### LY-MOD-043 — Support workbook sheet-name containment (`P2`)

```python
from openpyxl import Workbook

book = Workbook()
print("Sheet" in book)
```

**openpyxl 3.1.4:** `True`. **Lython:** rejects the expression statically because `Workbook` has no containment protocol. The workbook already supports iteration and `book[name]`; openpyxl's common sheet lookup contract also uses `name in book`.

### LY-MOD-044 — Honor openpyxl style constructor and property aliases (`P2`)

```python
from openpyxl.styles import Font

font = Font(name="Arial", bold=True, size=12)
print(font.size, font.sz, font.bold, font.b)
```

**openpyxl 3.1.4:** prints `12.0 12.0 True True`. **Lython:** rejects `size=` even though it accepts `sz=`, and its static member surface rejects `font.size` despite the runtime style record carrying that alias. The vanilla style subset should consistently expose common long/short aliases; the same audit found missing `Alignment` aliases such as `wrapText`, `textRotation`, and `shrinkToFit`/`shrink_to_fit`.

### LY-MOD-045 — Normalize six-digit openpyxl RGB colors to ARGB (`P1`)

```python
from openpyxl.styles.colors import Color

print(Color(rgb="FF0000").rgb)
```

**openpyxl 3.1.4:** prints `00FF0000`, inserting the default alpha byte. **Lython:** prints `FF0000` and serializes that unnormalized value. Common six-digit style colors are accepted by openpyxl but their public and package representation is eight-digit ARGB.

### LY-MOD-046 — Compare openpyxl style values structurally (`P1`)

```python
from openpyxl.styles import Font

print(Font(bold=True) == Font(bold=True))
print(Font(bold=True) == Font(bold=False))
```

**openpyxl 3.1.4:** `True` then `False`. **Lython:** `False` twice because style equality falls back to object identity. Styles are value-like descriptors; structural equality is used by ordinary scripts and is also important for deduplicating equivalent style records during workbook serialization.

### LY-MOD-047 — Preserve worksheet cell object identity and representation (`P1`)

```python
from openpyxl import Workbook

sheet = Workbook().active
print(sheet["A1"] is sheet.cell(1, 1))
print(repr(sheet["A1"]))
```

**openpyxl 3.1.4:** prints `True` and `<Cell 'Sheet'.A1>`. **Lython:** prints `False` and `<Cell 'A1'>`. Repeated access creates new public wrappers instead of returning the worksheet's cached cell object, and the representation loses its worksheet qualification; both are observable parts of the vanilla object model.

## Ordered commit progress

Each checkbox is one intended implementation commit, including regression tests derived from the referenced reproducer(s). Containment escapes come first, followed by shared protocols and small call-shape fixes, then data-model and serialization work, and finally the openpyxl object/style surface.

- [x] **Commit 01 — Bound JSON serialization recursion.** Keep recursion accounting active when `check_circular=False` so cycles fail inside Lython rather than overflowing the CLR stack. Addresses LY-MOD-034.
- [x] **Commit 02 — Contain empty pathlib inputs.** Represent empty/no-component paths as `.` and translate every path-construction failure through the Python failure boundary. Addresses LY-MOD-037.
- [x] **Commit 03 — Centralize and shape Lython version metadata.** Derive every version view from the declared target and give `version_info` its named fields. Addresses LY-MOD-021 and LY-MOD-042.
- [x] **Commit 04 — Correct the public regex flag map.** Separate `ASCII` from `UNICODE`, translate CPython bits at the backend boundary, and enforce ASCII character-class behavior. Addresses LY-MOD-019.
- [x] **Commit 05 — Complete operator protocol dispatch.** Honor `__length_hint__` and `__index__` with CPython validation and fallback semantics. Addresses LY-MOD-017 and LY-MOD-036.
- [x] **Commit 06 — Dispatch math conversion protocols.** Route rounding and integer-only helpers through the relevant Python special methods without regressing builtin numeric fast paths. Addresses LY-MOD-035.
- [x] **Commit 07 — Complete argparse token disambiguation.** Consume `--` as the option terminator and recognize unmatched negative numbers as positional values. Addresses LY-MOD-001 and LY-MOD-024.
- [x] **Commit 08 — Complete open-ended `itertools.islice`.** Represent a `None` stop without narrowing it to a machine integer and test both supported signatures. Addresses LY-MOD-014.
- [x] **Commit 09 — Enforce random population protocols.** Require sequence/indexable populations for `choice` and `sample` while retaining deterministic state behavior. Addresses LY-MOD-018.
- [x] **Commit 10 — Preserve atomic tuple identity in deep copies.** Reuse tuple inputs when every recursively copied element is unchanged, without regressing cycles or memo dictionaries. Addresses LY-MOD-004.
- [x] **Commit 11 — Complete `Counter` numeric/unary behavior.** Generalize supported numeric counts and implement unary positive/negative filtering with ordered results. Addresses LY-MOD-002 and LY-MOD-025.
- [x] **Commit 12 — Normalize deque rotations before conversion.** Reduce arbitrary-size rotation integers modulo the current length and retain empty-deque behavior. Addresses LY-MOD-003.
- [x] **Commit 13 — Complete `ChainMap`'s mapping protocol.** Count the union of visible keys and pin length alongside iteration, lookup, and mutation. Addresses LY-MOD-026.
- [x] **Commit 14 — Generate dataclass representations with `repr`.** Render every generated field through the shared representation protocol. Addresses LY-MOD-006.
- [x] **Commit 15 — Generalize recursive dataclass dictionary conversion.** Preserve supported key objects and recursively copy keys and values in `asdict`. Addresses LY-MOD-007.
- [x] **Commit 16 — Stabilize dataclass field/error objects.** Reuse class-owned `Field` records and leave invalid `replace` overrides as catchable runtime `ValueError`s. Addresses LY-MOD-027 and LY-MOD-028.
- [x] **Commit 17 — Separate datetime display and representation paths.** Implement Python-shaped `timedelta.__str__` and `datetime.__str__`, and retain `tzinfo`/`fold` in aware representations. Addresses LY-MOD-008, LY-MOD-009, and LY-MOD-029.
- [x] **Commit 18 — Preserve second/microsecond fixed offsets.** Remove the whole-minute timezone restriction and extend ISO offset formatting/parsing tests. Addresses LY-MOD-010.
- [x] **Commit 19 — Complete fixed-offset ISO parsing.** Accept CPython's calendar basic, week-date, compact-time, and leading-`T` forms without adding locale or IANA dependencies. Addresses LY-MOD-030.
- [x] **Commit 20 — Route decimal rounding through the active context.** Apply exported rounding modes consistently to supported rounding and integral conversion operations. Addresses LY-MOD-011.
- [x] **Commit 21 — Retain decimal quantum metadata.** Preserve representable exponent/scale information through construction, rendering, tuple conversion, copying, and arithmetic where Python retains it. Addresses LY-MOD-012.
- [x] **Commit 22 — Encode empty CSV fields without losing row shape.** Quote the single-empty-field case and retain correct character counts for file-backed and in-memory writers. Addresses LY-MOD-005.
- [x] **Commit 23 — Fix regex empty-match progression.** Permit adjacent non-empty matches at the same position across find, substitute, and count paths. Addresses LY-MOD-038.
- [x] **Commit 24 — Share singledispatch's MRO resolver.** Make public `dispatch(cls)` select exactly as ordinary decorated invocation does. Addresses LY-MOD-031.
- [x] **Commit 25 — Make `cmp_to_key` wrappers comparable.** Implement the comparator-backed rich comparison surface directly on wrapper values. Addresses LY-MOD-032.
- [x] **Commit 26 — Unify JSON dictionary-key handling.** Use canonical float spellings and apply `sort_keys` to original keys before their JSON string conversion. Addresses LY-MOD-015 and LY-MOD-016.
- [x] **Commit 27 — Pin JSON lexer error coordinates.** Report the offending code-point position consistently for incomplete numbers, bad escapes, and surrounding structural failures. Addresses LY-MOD-033.
- [x] **Commit 28 — Make statistical modes type-generic.** Count supported hashable observations without routing through numeric aggregate coercion. Addresses LY-MOD-020.
- [x] **Commit 29 — Complete statistics aggregate signatures.** Add weighted `fmean` and optional supplied-center arguments to variance and standard-deviation helpers. Addresses LY-MOD-039 and LY-MOD-040.
- [x] **Commit 30 — Complete the supported `NormalDist` methods.** Add deterministic `zscore` behavior and its zero-sigma failure contract. Addresses LY-MOD-041.
- [x] **Commit 31 — Normalize typing inspection aliases.** Map collection aliases to builtin origins and normalize `Optional`/`Union` origins and argument tuples while retaining inert annotations. Addresses LY-MOD-022.
- [ ] **Commit 32 — Align openpyxl named-style visibility.** Return public style-name strings from `named_styles` and keep object access on the private style collection. Addresses LY-MOD-023.
- [ ] **Commit 33 — Complete openpyxl style aliases.** Accept and expose the common long/short `Font` and `Alignment` names consistently in runtime and static contracts. Addresses LY-MOD-044.
- [ ] **Commit 34 — Normalize openpyxl ARGB colors.** Canonicalize accepted six-digit RGB inputs before public access and package serialization. Addresses LY-MOD-045.
- [ ] **Commit 35 — Give openpyxl styles value equality.** Compare supported style descriptors structurally and reuse that equality for style-table deduplication. Addresses LY-MOD-046.
- [ ] **Commit 36 — Complete workbook/cell object protocols.** Add sheet-name containment, cache public cell objects per coordinate, and include worksheet qualification in cell representations. Addresses LY-MOD-043 and LY-MOD-047.
