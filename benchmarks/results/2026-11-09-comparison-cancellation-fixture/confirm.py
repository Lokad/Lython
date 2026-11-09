from pathlib import Path
import hashlib,json,os,subprocess
root=Path(__file__).resolve().parent
repo=Path('D:/code/Lython')
env=os.environ.copy();env['DOTNET_ROOT']='C:/Users/JoannesVermorel/.dotnet11';env['PATH']=env['DOTNET_ROOT']+os.pathsep+'C:/Python313'+os.pathsep+env['PATH']
checks={}
for name in ['original','corrected']:
    folder=root/name;folder.mkdir(exist_ok=False)
    source=(root/'original.cs' if name=='original' else repo/'tests/Lokad.Lython.Tests/ScalarComparisonCancellationTests.cs').read_text(encoding='utf-8')
    assert source.count('Thread.Sleep(1);')==1
    # A controlled snapshot delays only the cancelling worker, never the operation.
    source=source.replace('Thread.Sleep(1);','Thread.Sleep(2000);')
    (folder/'ScalarComparisonCancellationTests.cs').write_text(source,encoding='utf-8')
    props=folder/'Directory.Build.props'
    props.write_text(f'''<Project>
 <Import Project="{repo}/Directory.Build.props" />
 <PropertyGroup>
  <BaseOutputPath>{folder}/out/$(MSBuildProjectName)/</BaseOutputPath>
  <BaseIntermediateOutputPath>{folder}/obj/$(MSBuildProjectName)/</BaseIntermediateOutputPath>
  <DefaultItemExcludes>$(DefaultItemExcludes);$(MSBuildProjectDirectory)/bin/**;$(MSBuildProjectDirectory)/obj/**</DefaultItemExcludes>
  <NuGetLockFilePath>{folder}/locks/$(MSBuildProjectName).json</NuGetLockFilePath>
 </PropertyGroup>
 <PropertyGroup Condition="'$(MSBuildProjectName)' == 'Lokad.Lython.Tests'">
  <DefaultItemExcludes>$(DefaultItemExcludes);ScalarComparisonCancellationTests.cs</DefaultItemExcludes>
 </PropertyGroup>
 <ItemGroup Condition="'$(MSBuildProjectName)' == 'Lokad.Lython.Tests'">
  <Compile Include="{folder}/ScalarComparisonCancellationTests.cs" Link="ScalarComparisonCancellationTests.cs" />
 </ItemGroup>
</Project>
''',encoding='utf-8')
    args=[env['DOTNET_ROOT']+'/dotnet.exe','test','tests/Lokad.Lython.Tests','-c','Release','-f','net10.0','--filter','FullyQualifiedName~ScalarComparisonCancellationTests&Category=Stress',f'-p:DirectoryBuildPropsPath={props}']
    with (folder/'test.log').open('w',encoding='utf-8') as log:
        run=subprocess.run(args,cwd=repo,env=env,stdout=log,stderr=subprocess.STDOUT)
    result=(folder/'test.log').read_text(encoding='utf-8')
    print(name,run.returncode,result[-1800:],flush=True)
    if name=='original':
        assert run.returncode!=0 and result.count('cancellation requested before completion: False')>=2
    else:assert run.returncode==0 and 'Passed:     2' in result
    dll=folder/'out/Lokad.Lython.Tests/Release/net10.0/Lokad.Lython.WhiteBox.Tests.dll'
    checks[name]=dict(exitCode=run.returncode,sourceSHA256=hashlib.sha256(source.encode()).hexdigest(),assemblySHA256=hashlib.sha256(dll.read_bytes()).hexdigest())
(root/'confirmation.json').write_text(json.dumps(dict(checks=checks,conditions='Isolated snapshot builds; only canceller sleep changed from 1ms to 2000ms to reproduce legal completion before request. Normal fixture still 1ms, 256MiB, 10s timeout. No production source change.'),indent=2)+'\n',encoding='utf-8')
