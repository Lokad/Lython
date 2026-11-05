from pathlib import Path
import json,shutil
root=Path(__file__).resolve().parent
old=root.parent/'native-preparation-b246'
remote='/home/lokad/lython-bench-checks/native-confirm-b247-20261105'
unit='lython-b247-native-confirm-20261105'
for name in ['setup.py','launch.py','archive.py','collect.sh','assess.py']:
    text=(old/name).read_text(encoding='utf-8').replace('/home/lokad/lython-bench-checks/native-preparation-b246-20261105',remote).replace(
        'lython-b246-native-preparation-20261105',unit).replace('b246-payload.tgz','b247-payload.tgz').replace('RuntimeMaxSec=300','RuntimeMaxSec=120')
    if name=='archive.py':text=text.replace("len(r['observations'])==10","len(r['observations'])==2")
    if name=='assess.py':text=text.replace('ten complete-script/control jobs','two same-bit CSV/JSON confirmation jobs')
    (root/name).write_text(text,encoding='utf-8',newline='\n')
for name in ['candidate-identity.json','source.patch','source-snapshot.tgz','hash-witness.json','reference-check.py']:
    shutil.copyfile(old/name,root/name)
shutil.copyfile(root/'plan.md',root/'pre-measurement-plan.md')
(root/'candidate').mkdir(exist_ok=True)
shutil.copyfile(old/'candidate/Lokad.Lython.dll',root/'candidate/Lokad.Lython.dll')
catalog=json.loads((old/'catalog.json').read_text(encoding='utf-8'))
cases=[c for c in catalog['cases'] if c['id'] in ['scripts.csv-summary-helpers.large','scripts.json-summary-helpers.large']]
assert len(cases)==2
catalog['cases']=cases
(root/'catalog.json').write_text(json.dumps(catalog,ensure_ascii=False)+'\n',encoding='utf-8')
(root/'jobs').mkdir(exist_ok=True)
program=(old/'jobs/Program.cs').read_text(encoding='utf-8')
a=program.index('if (cases.Length != 10');b=program.index('var observations',a)
expected=',\n'.join('        '+json.dumps(c['id']+'/'+c['mode']) for c in sorted(cases,key=lambda c:c['id']+'/'+c['mode']))
program=program[:a]+'''if (cases.Length != 2 || !cases.Select(x=>x.GetProperty("id").GetString()+"/"+x.GetProperty("mode").GetString()).Order(StringComparer.Ordinal).SequenceEqual(new[] {
'''+expected+''' }))
    throw new InvalidDataException("Only two frozen same-bit CSV/JSON confirmation jobs are allowed.");
'''+program[b:]
program=program.replace('''    using var before = new Image(beforePath, item, hashSource, "before");
    using var candidate = new Image(candidatePath, item, hashSource, "candidate");
    using var control = new Image(beforePath, item, hashSource, "before-control");''','''    using var control = new Image(beforePath, item, hashSource, "before-control");
    using var candidate = new Image(candidatePath, item, hashSource, "candidate");
    using var before = new Image(beforePath, item, hashSource, "before");''')
program=program.replace('caseNumber++ % 2 == 0 ? images : images.Reverse()', 'caseNumber++ % 2 == 0 ? images.Reverse() : images')
program=program.replace('warmupMs < 6000','warmupMs < 10000').replace('200.0 * warmupBatch','250.0 * warmupBatch').replace('warmupGuestSecondsPerImage = 6','warmupGuestSecondsPerImage = 10').replace('targetBatchMilliseconds = 200','targetBatchMilliseconds = 250')
program=program.replace('Complete prepared native bodies versus cf3dc9cd; CSV/JSON warm, text/report warm/async/fresh, textcompile and ordinary call control',
    'Same prepared-native bits versus cf3dc9cd; bounded CSV/JSON confirmation of original large control spreads')
(root/'jobs/Program.cs').write_text(program,encoding='utf-8')
shutil.copyfile(old/'jobs/complete-jobs.csproj',root/'jobs/complete-jobs.csproj')
print('Two unchanged-bit confirmation cases; original source/results preserved')
