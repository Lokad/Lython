from pathlib import Path
import base64,hashlib,json,shutil,struct,subprocess,tarfile
root=Path(__file__).resolve().parent
old=root.parent/'json-string-runs-b244'
remote='/home/lokad/lython-bench-checks/json-output-coverage-b245-20261105'
unit='lython-b245-json-output-coverage-20261105'
for name in ['setup.py','launch.py','archive.py','collect.sh','assess.py']:
    text=(old/name).read_text().replace('/home/lokad/lython-bench-checks/json-string-runs-b244-20261105',remote).replace(
        'lython-b244-json-string-runs-20261105',unit).replace('b244-payload.tgz','b245-payload.tgz').replace('RuntimeMaxSec=240','RuntimeMaxSec=150')
    if name=='archive.py':text=text.replace("len(r['observations'])==6","len(r['observations'])==3")
    if name=='assess.py':text=text.replace('six complete-script report/JSON/text-control jobs','three output-coverage/unchanged-text jobs')
    (root/name).write_text(text,encoding='utf-8',newline='\n')
for name in ['candidate-identity.json','source.patch','source-snapshot.tgz','hash-witness.json','reference-check.py']:
    shutil.copyfile(old/name,root/name)
shutil.copyfile(root/'plan.md',root/'pre-measurement-plan.md')
(root/'candidate').mkdir(exist_ok=True)
shutil.copyfile(old/'candidate/Lokad.Lython.dll',root/'candidate/Lokad.Lython.dll')
rows=[dict(id=i,text='aλ😀',active=i%2==0) for i in range(1024)]
original=json.dumps(rows,ensure_ascii=True,separators=(',',':')).encode()
for row in rows:row['active']=not row['active']
output=json.dumps(rows,ensure_ascii=True,separators=(',',':')).encode()
fixture=json.dumps(dict(size=1024,text=None,inputFiles={'input.json':base64.b64encode(original).decode()},
    outputFiles={'output.json':base64.b64encode(output).decode()}),separators=(',',':'))
def digest(files):
    h=hashlib.sha256();h.update(struct.pack('>i',len(files)))
    for name,data in sorted(files.items()):
        name=name.encode();h.update(struct.pack('>i',len(name)));h.update(name);h.update(struct.pack('>i',len(data)));h.update(data)
    return h.hexdigest()
catalog=json.loads((old/'catalog.json').read_text());cases=[]
for route in ['encode','stream']:
    source="import json\nwith open('input.json') as source:\n    values=json.load(source)\nfor row in values:\n    row['active']=not row['active']\nwith open('output.json','w',newline='') as target:\n"
    source += ("    target.write(json.dumps(values,ensure_ascii=True,separators=(',',':')))\n" if route=='encode' else
               "    json.dump(values,target,ensure_ascii=True,separators=(',',':'))\n")
    source += 'print(len(values))\n'
    case=dict(id='diagnostic.json-record-write-'+route+'.large',family='json',category='output-qualification',scale='large',size=1024,
        source=source,fixtureJson=fixture,expectedOutput='1024\n',sourceUtf8Bytes=len(source.encode()),expectedOutputUtf8Bytes=5,
        fixtureTextUtf8Bytes=0,fixtureTextCodePoints=0,sourceSha256=hashlib.sha256(source.encode()).hexdigest(),
        fixtureSha256=hashlib.sha256(fixture.encode()).hexdigest(),expectedOutputSha256=hashlib.sha256(b'1024\n').hexdigest(),
        expectedFilesSha256=digest({'output.json':output}),mode='warm')
    cases.append(case)
cases.append(next(c for c in catalog['cases'] if c['id']=='scripts.text-summary-helpers.large'))
catalog['cases']=cases
(root/'catalog.json').write_text(json.dumps(catalog,ensure_ascii=False)+'\n',encoding='utf-8')
(root/'jobs').mkdir(exist_ok=True)
program=(old/'jobs/Program.cs').read_text()
start=program.index('if (cases.Length != 6');end=program.index('var observations',start)
expected=',\n'.join('        '+json.dumps(c['id']+'/'+c['mode']) for c in sorted(cases,key=lambda c:c['id']+'/'+c['mode']))
program=program[:start]+'''if (cases.Length != 3 || !cases.Select(x=>x.GetProperty("id").GetString()+"/"+x.GetProperty("mode").GetString()).Order(StringComparer.Ordinal).SequenceEqual(new[] {
'''+expected+''' }))
    throw new InvalidDataException("Only three frozen JSON output-coverage/control jobs are allowed.");
'''+program[end:]
program=program.replace('Complete JSON native byte-run reader/encoder versus cf3dc9cd; report warm/async/fresh/compile, JSON warm and text control',
    'Completed JSON byte-run source versus cf3dc9cd;1024-row encode/stream output qualification plus text control')
(root/'jobs/Program.cs').write_text(program,encoding='utf-8')
shutil.copyfile(old/'jobs/complete-jobs.csproj',root/'jobs/complete-jobs.csproj')
print(json.dumps(dict(inputBytes=len(original),outputBytes=len(output),candidateCoreSHA256=hashlib.sha256((root/'candidate/Lokad.Lython.dll').read_bytes()).hexdigest(),jobs=len(cases))))
