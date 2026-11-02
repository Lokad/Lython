import base64, contextlib, copy, hashlib, io, json, pathlib, struct, subprocess, tempfile

root = pathlib.Path(__file__).parent
prior = root.parent / 'native-text-admission-b111'
catalog = json.loads((root/'catalog.json').read_text(encoding='utf-8-sig'))
cases = [c for c in catalog['cases'] if c['id'] in {
    'scripts.file-search.large', 'scripts.text-summary-helpers.large',
    'scripts.csv-summary-helpers.large', 'scripts.json-summary-helpers.large'}]
report = copy.deepcopy(next(c for c in cases if c['id']=='scripts.text-summary-helpers.large'))
report['id']='scripts.native-helper-report.large'
report['source']='''def normalize(value):
    return value.strip().lower().replace('café','coffee')
def count_words(value):
    return len(value.split())
def render(value, count):
    return value.upper()+'\\t'+str(count)+'\\n'
rows = 0
with open('input.txt') as source:
    with open('report.txt','w',newline='') as target:
        for line in source:
            normalized = normalize(line)
            target.write(render(normalized, count_words(normalized)))
            rows += 1
print(rows)
'''
fixture=json.loads(report['fixtureJson'])
with tempfile.TemporaryDirectory(prefix='lython-b113-golden-') as directory:
    directory=pathlib.Path(directory)
    for name, data in fixture['inputFiles'].items():
        target=directory/name
        target.parent.mkdir(parents=True,exist_ok=True)
        target.write_bytes(base64.b64decode(data))
    completed=subprocess.run(['C:/Python313/python.exe','-I','-S','-X','utf8','-c',report['source']],
        cwd=directory,capture_output=True,check=True,timeout=10)
    assert not completed.stderr
    report['expectedOutput']=completed.stdout.decode().replace('\r\n','\n')
    fixture['outputFiles']={name:base64.b64encode((directory/name).read_bytes()).decode() for name in fixture['outputFiles']}
def digest(files):
    h=hashlib.sha256()
    h.update(struct.pack('>i',len(files)))
    for name,data in sorted(files.items()):
        path=name.encode(); data=base64.b64decode(data)
        h.update(struct.pack('>i',len(path)));h.update(path)
        h.update(struct.pack('>i',len(data)));h.update(data)
    return h.hexdigest()
report['fixtureJson']=json.dumps(fixture,separators=(',',':'),ensure_ascii=False)
report['expectedFilesSha256']=digest(fixture['outputFiles'])
for key, value in [('source',report['source']),('expectedOutput',report['expectedOutput']),('fixture',report['fixtureJson'])]:
    report[key+'Sha256']=hashlib.sha256(value.encode()).hexdigest()
report['sourceUtf8Bytes']=len(report['source'].encode())
report['expectedOutputUtf8Bytes']=len(report['expectedOutput'].encode())
cases.append(report)
catalog['cases']=cases
(root/'measurement-catalog.json').write_text(json.dumps(catalog,ensure_ascii=False,indent=2),encoding='utf-8')
(root/'hash-witness.json').write_bytes((prior/'hash-witness.json').read_bytes())
jobs=root/'jobs';jobs.mkdir(exist_ok=True)
(jobs/'complete-jobs.csproj').write_bytes((prior/'jobs/complete-jobs.csproj').read_bytes())
source=(prior/'jobs/Program.cs').read_text(encoding='utf-8-sig')
start=source.index('if (cases.Length != 6')
end=source.index('var observations',start)
source=source[:start]+'''if (cases.Length != 5 || !cases.Select(x => x.GetProperty("id").GetString()).Order().SequenceEqual(
    new[] { "scripts.csv-summary-helpers.large", "scripts.file-search.large", "scripts.json-summary-helpers.large", "scripts.native-helper-report.large", "scripts.text-summary-helpers.large" }))
    throw new InvalidDataException("Require complete text/search/helper report and CSV/JSON controls.");
'''+source[end:]
source=source.replace('Complete native whitespace construction/admission','Complete prepared native helper expressions')
# Predeclare 192 warmups + nine32-job blocks: five complete scripts, three
# isolated images, two orders. Not ten-minute lanes; retain all raw observations.
source=source.replace('i < 256','i < 192').replace('i < 64','i < 32').replace('jobs = 64','jobs = 32')
source=source.replace('warmupsPerImage = 256','warmupsPerImage = 192').replace('batch = 64','batch = 32')
# Reuse the same driver for fresh public compile-to-result and compile-only.
# Full goldens still run before/after every phase; compilation is never asked
# to produce runtime output (the earlier B112 supervisor lesson).
source=source.replace('var observations = new List<object>();', '''var phase = args.Length > 5 ? args[5] : "warm";
if (phase is not ("warm" or "fresh" or "compile")) throw new InvalidDataException("Unknown phase.");
var warmups = phase == "warm" ? 192 : 96;
var batch = phase == "compile" ? 256 : phase == "fresh" ? 24 : 32;
var observations = new List<object>();''')
source=source.replace('for (var i = 0; i < 192; i++) foreach (var image in images) image.Once();',
    'for (var i = 0; i < warmups; i++) foreach (var image in images) image.Measure(phase);')
source=source.replace('for (var i = 0; i < 32; i++) image.Once();','for (var i = 0; i < batch; i++) image.Measure(phase);')
source=source.replace('jobs = 32','jobs = batch').replace('warmupsPerImage = 192','phase, warmupsPerImage = warmups').replace('batch = 32, observations','batch, observations')
source=source.replace('private readonly object _script;', 'private object _script;\n    private readonly object _engine;\n    private readonly MethodInfo _compile;\n    private readonly string _source;')
source=source.replace('var engine = Activator.CreateInstance(EngineType)!;', 'var engine = Activator.CreateInstance(EngineType)!;\n        _engine = engine;\n        _source = item.GetProperty("source").GetString()!;')
needle='var fixtureType = benchmark.GetType('
source=source.replace(needle,'_compile = compile;\n        '+needle)
needle='    public void Once()'
source=source.replace(needle,'''    public void Measure(string phase)
    {
        if (phase == "warm") { Once(); return; }
        var original = _script;
        var expected = _expected;
        using (var fresh = (IDisposable)_compile.Invoke(_engine, [_source, _budget])!)
        {
            RequireValid(fresh);
            if (phase == "fresh")
            {
                _script = fresh;
                _expected = -1;
                try { Once(); }
                finally { _script = original; _expected = expected; }
            }
        }
        if (expected >= 0 && Balance != expected) throw new InvalidDataException("Fresh ownership did not end.");
    }
'''+needle)
(jobs/'Program.cs').write_text(source,encoding='utf-8')
print(json.dumps({'cases':[c['id'] for c in cases], 'reportGoldenCPython':'3.13.2',
    'reportStdout':report['expectedOutput'],'reportFileHash':report['expectedFilesSha256'],
    'reportSourceSHA256':report['sourceSha256']}))
