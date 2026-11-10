from pathlib import Path
import copy,hashlib,json,shutil

root=Path(__file__).resolve().parent
prior=root.parent/'regex-scalar-cursor-r40'
repo=Path('D:/code/Lython')
tool=root/'tool';tool.mkdir()
for name in ['Directory.Build.props','complete-jobs.csproj']:
    shutil.copyfile(prior/'tool'/name,tool/name)
source=(prior/'tool/Program.cs').read_text(encoding='utf-8-sig')
source=source.replace('Require four Unicode scaling cases and ASCII/forward controls.','Require six UTF-8 admission/control diagnostics.')
source=source.replace('kv.Key != "Lokad.Lython.dll"','kv.Key != "Lokad.Lython.Utf8Regex.dll"')
source=source.replace('var warmups = 4;','var warmups = 32;').replace('var batch = 1;','var batch = 4;')
source=source.replace('warmupsPerImage = warmups, settleSecondsPerCase = .2, rounds = 5, batch','warmupsPerImage = warmups, settleSecondsPerCase = .2, rounds = 5, batch')
source=source.replace('R40 scalar projection diagnostic','R215 same-producer trusted-input diagnostic')
(tool/'Program.cs').write_text(source,encoding='utf-8',newline='\n')
catalog=json.loads((prior/'catalog.json').read_text(encoding='utf-8-sig'))
template=catalog['cases'][0]
items=[
 ('ascii-prefix',"import re\ntext='a'*8192\np=re.compile('^a')\nprint(sum(1 for i in range(128) if p.search(text)))\n",'128\n'),
 ('unicode-prefix',"import re\ntext='α'*8192\np=re.compile('^α')\nprint(sum(1 for i in range(128) if p.search(text)))\n",'128\n'),
 ('ascii-prefix-miss',"import re\ntext='a'*8192\np=re.compile('^z')\nprint(sum(1 for i in range(128) if p.search(text)))\n",'0\n'),
 ('unicode-lookbehind',"import re\ntext='α'*1024\nprint(sum(m.start(1)+m.end(2) for m in re.finditer('(?<=(..))(.)',text)))\n",str(1024*1022)+'\n'),
 ('unicode-forward',"import re\ntext='α'*4096\nprint(sum(m.start(1)+m.end(2) for m in re.finditer('(.)(.)',text)))\n",str(4096*4096//2)+'\n'),
 ('non-regex-control',"text='alpha beta gamma '*1024\nprint(sum(len(word) for word in text.split()))\n",str(14*1024)+'\n')]
cases=[]
for name,source,golden in items:
    case=copy.deepcopy(template);case['id']='diagnostic.utf8.'+name
    case['family']='regex-valid-input';case['category']='diagnostic';case['size']=8192
    case['source']=source;case['expectedOutput']=golden
    case['fixtureJson']=json.dumps(dict(size=8192,text=None,inputFiles={},outputFiles={}),separators=(',',':'))
    for field in ['source','expectedOutput']:
        case[field+'Utf8Bytes']=len(case[field].encode('utf-8'))
        case[field+'Sha256']=hashlib.sha256(case[field].encode('utf-8')).hexdigest()
    case['fixtureSha256']=hashlib.sha256(case['fixtureJson'].encode('utf-8')).hexdigest()
    case['fixtureTextUtf8Bytes']=0;case['fixtureTextCodePoints']=0
    case['expectedFilesSha256']=hashlib.sha256(bytes(4)).hexdigest()
    cases.append(case)
catalog['cases']=cases
(root/'catalog.json').write_text(json.dumps(catalog,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
aux=repo/'benchmarks/Lokad.Lython.Benchmarks/bin/Release/net11.0'
trusted=root/'out/Lokad.Lython.PublicApi.Tests/Release/net11.0'
baseline=root.parent/'regex-dev67-r215/out/Lokad.Lython.PublicApi.Tests/Release/net11.0'
images=root/'images';images.mkdir()
for role,adapter in [('before',baseline/'Lokad.Lython.Utf8Regex.dll'),('candidate',trusted/'Lokad.Lython.Utf8Regex.dll')]:
    target=images/role;target.mkdir()
    for path in aux.iterdir():
        if path.is_file() and path.suffix!='.pdb': shutil.copyfile(path,target/path.name)
    for name in ['Lokad.Lython.dll','Lokad.Utf8Regex.dll','Lokad.Utf8Regex.PythonRe.dll']:
        shutil.copyfile(trusted/name,target/name)
    shutil.copyfile(adapter,target/adapter.name)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
identity={role:{p.name:sha(p)for p in (images/role).glob('*.dll')}for role in ['before','candidate']}
assert [n for n in identity['before'] if identity['before'][n]!=identity['candidate'][n]]==['Lokad.Lython.Utf8Regex.dll']
(root/'comparison-identity.json').write_text(json.dumps(identity,indent=2)+'\n',encoding='utf-8')
folder=root/'diagnostic';folder.mkdir()
(folder/'hash-witness.json').write_text(json.dumps(dict(source="return [hash('Lython-é😀'), hash((1, 2.0, True))]\n"),ensure_ascii=False)+'\n',encoding='utf-8')
print('Prepared existing six-case diagnostic driver; only adapter differs between images.',flush=True)
