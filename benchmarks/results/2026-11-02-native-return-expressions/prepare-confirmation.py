import hashlib,json,pathlib
root=pathlib.Path(__file__).parent
old=root.parent/'representative-b112'
text=(old/'compile-followup.py').read_text(encoding='utf-8-sig')
text=text.replace('representative-b112-20261102','call-construction-b113-refined-20261102')
text=text.replace("catalog = root / 'representative.json'","catalog = root / 'measurement-catalog.json'")
text=text.replace("out = root / 'compile-followup'","out = root / 'compile-conditioned'")
text=text.replace("['scripts.csv-summary-helpers.large', 'scripts.report-render.large']",
    "['scripts.csv-summary-helpers.large', 'scripts.json-summary-helpers.large', 'scripts.native-helper-report.large']")
text=text.replace("str(worker), '--compare', 'worker'", "str(root/('candidate' if label == 'candidate' else 'baseline')/'Lokad.Lython.Benchmarks.dll'), '--compare', 'worker'")
text=text.replace("identity['files']['Lokad.Lython.dll'].lower()", "identity['coreSHA256'][label].lower()")
text=text.replace("['baseline','candidate','CPython']", "['before','candidate','before-control']")
text=text.replace('affected representative checkpoint af82bdfc runtimefacca3e9','B113 conditioned compilation correction, original adverse unconditioned data preserved')
text=text.replace('targetBatchSeconds=.2','targetBatchSeconds=.1').replace('math.ceil(count*.2/','math.ceil(count*.1/')
(root/'compile-conditioned.py').write_text(text,encoding='utf-8')
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
identity={'sourceRevision':'8f58934c + uncommitted B113 refined source',
    'baselineRuntime':'bc0d9823, fully qualified41460677410',
    'coreSHA256':{'before':sha(root/'baseline/Lokad.Lython.dll'),
        'before-control':sha(root/'baseline/Lokad.Lython.dll'),
        'candidate':sha(root/'refined/candidate/Lokad.Lython.dll')},
    'benchmarkDllSHA256':sha(root/'baseline/Lokad.Lython.Benchmarks.dll'),
    'originalCoreSHA256':sha(root/'candidate/Lokad.Lython.dll'),
    'boundaries':'Warm/fresh include fixture reset, mediated real I/O, full stdout/file goldens, result disposal and balance checks. Fresh includes compilation/disposal. Compile phase excludes guest execution/I/O/result cleanup, includes compiled ownership disposal. No startup timing.',
    'conditioningCorrection':'Original fixed96 compile warmup was ~10ms and showed controls up70% and candidate JSON/report3-5x; unresolved, preserved. Only final refined pair receives existing timed4s persistent-worker compilation qualification; no claim that the original regression is disproved.'}
(root/'identity.json').write_text(json.dumps(identity,indent=2),encoding='utf-8')
