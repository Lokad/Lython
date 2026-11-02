import base64,copy,hashlib,json,pathlib,struct,subprocess,tempfile,shutil
root=pathlib.Path(__file__).parent
old=root.parent/'native-mapping-writes-b117'
catalog=json.loads((old/'catalog.json').read_text(encoding='utf-8-sig'))
keep={'scripts.csv-summary-helpers.large','scripts.text-summary-helpers.large','scripts.native-helper-report.large'}
cases=[c for c in catalog['cases'] if c['id'] in keep]
upper=json.loads((root.parent/'owned-output-b109/catalog.json').read_text(encoding='utf-8-sig'))
upper=next(c for c in upper['cases'] if c['id']=='scripts.text-uppercase.large')
cases.append(upper)
def digest(files):
    h=hashlib.sha256();h.update(struct.pack('>i',len(files)))
    for name,data in sorted(files.items()):
        key=name.encode();data=base64.b64decode(data)
        h.update(struct.pack('>i',len(key)));h.update(key);h.update(struct.pack('>i',len(data)));h.update(data)
    return h.hexdigest()
for name,text in [('scripts.ascii-uppercase.large','alpha BETA 42 _\t. xyz!\n'*2048),('scripts.unicode-uppercase.large','Ω'*32768)]:
    case=copy.deepcopy(upper);case['id']=name;case['size']=len(text)
    fixture=json.loads(case['fixtureJson']);fixture['size']=len(text);fixture['inputFiles']={'input.txt':base64.b64encode(text.encode()).decode()}
    with tempfile.TemporaryDirectory(prefix='lython-b118-golden-') as folder:
        folder=pathlib.Path(folder);(folder/'input.txt').write_bytes(text.encode())
        result=subprocess.run(['C:/Python313/python.exe','-I','-S','-X','utf8','-c',case['source']],cwd=folder,check=True,capture_output=True,timeout=10)
        assert not result.stderr
        case['expectedOutput']=result.stdout.decode().replace('\r\n','\n')
        fixture['outputFiles']={'report.txt':base64.b64encode((folder/'report.txt').read_bytes()).decode()}
    case['fixtureJson']=json.dumps(fixture,separators=(',',':'))
    case['expectedFilesSha256']=digest(fixture['outputFiles'])
    for key in ['source','expectedOutput','fixture']:
        value=case['fixtureJson'] if key=='fixture' else case[key]
        case[key+'Sha256']=hashlib.sha256(value.encode()).hexdigest()
    case['expectedOutputUtf8Bytes']=len(case['expectedOutput'].encode())
    cases.append(case)
catalog['cases']=cases;(root/'catalog.json').write_text(json.dumps(catalog,ensure_ascii=False,indent=2),encoding='utf-8')
text=(old/'collect.py').read_text()
text=text.replace('native-mapping-writes-b117-20261102','bulk-ascii-casing-b118-20261102')
start=text.index('jobs = [(');end=text.index('env = os.environ.copy()',start)
text=text[:start]+"jobs = [(case,'warm') for block in range(2) for case in "+repr([c['id'] for c in cases])+']\n\n'+text[end:]
text=text.replace("            if case_id == 'scripts.csv-summary-helpers.large': labels += ['legacy','legacy-control']\n",'')
text=text.replace('if job_index >= 4:', 'if job_index >= 6:').replace('block=job_index//4,','block=job_index//6,')
text=text.replace('B117 native mapping writes; two complete source-pair blocks; external process CPU through response; no guest-only or allocation claim',
    'B118 bulk ASCII casing; complete scripts, Unicode/CSV controls; external process CPU through response; no guest-only or allocation claim')
(root/'collect.py').write_text(text,encoding='utf-8')
shutil.copy2(old/'cpython-worker.py',root/'cpython-worker.py')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
identity={'sourceRevision':'ffa3f6f8 + uncommitted B118 bounded ASCII mapping',
    'baselineRuntime':'ffa3f6f8',
    'coreSHA256':{label:sha(root/('candidate' if label=='candidate' else 'baseline')/'Lokad.Lython.dll') for label in ['baseline','candidate','before-control']},
    'cpuMetric':'External Linux/proc TGID user+system ticks before command/after response; weighted9-batch means, runtime/JIT/response included. No guest-only or allocation measurement.',
    'boundary':'Existing warm protocol includes reset, actual mediated I/O, full output/file checks and previous-result cleanup; last result/final compiled/process/fixture cleanup outside batch and joined. Compilation/startup excluded.'}
(root/'identity.json').write_text(json.dumps(identity,indent=2),encoding='utf-8')
