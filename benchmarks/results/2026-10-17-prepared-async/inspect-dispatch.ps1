$ErrorActionPreference='Stop'
$path=(Resolve-Path 'tests/Lokad.Lython.Tests/bin/Release/net10.0/Lokad.Lython.WhiteBox.Tests.dll').Path
$assembly=[Reflection.Assembly]::LoadFrom($path)
$type=$assembly.GetType('Lokad.Lython.Runtime.LythonRuntime',$true)
$method=@($type.GetMethods([Reflection.BindingFlags]'Static,NonPublic')|Where-Object { $_.Name -eq 'InvokeCallableTargetAsync' -and $_.IsGenericMethodDefinition })[0]
$attribute=$method.GetCustomAttributes([Runtime.CompilerServices.AsyncStateMachineAttribute],$false)[0]
$state=$attribute.StateMachineType
$move=$state.GetMethod('MoveNext',[Reflection.BindingFlags]'Instance,NonPublic')
$code=$move.GetMethodBody().GetILAsByteArray()
$opcodes=@{}
foreach($field in [Reflection.Emit.OpCodes].GetFields([Reflection.BindingFlags]'Public,Static')) {
 $op=$field.GetValue($null);$opcodes[([int]$op.Value -band 65535)]=$op
}
$offset=0;$typed=@();$previous="start"
while($offset -lt $code.Length) {
 $opcode=[int]$code[$offset];$offset++
 if($opcode -eq 254){$opcode=65024 -bor [int]$code[$offset];$offset++}
 $op=$opcodes[$opcode]
 if($null -eq $op){throw "Unknown opcode $opcode at $offset after $previous"}
 $previous=$op.Name+"@"+$offset
 $size=switch($op.OperandType.ToString()) {
  'InlineNone'{0} 'ShortInlineI'{1} 'ShortInlineVar'{1} 'ShortInlineBrTarget'{1} 'InlineVar'{2}
  'InlineI8'{8} 'InlineR'{8}
  'InlineSwitch'{4+4*[BitConverter]::ToInt32($code,$offset)}
  default{4}
 }
 if($op.OperandType.ToString() -eq 'InlineType') {
  $token=[BitConverter]::ToInt32($code,$offset)
  $resolved=$move.Module.ResolveType($token,$state.GetGenericArguments(),$null)
  $typed += [pscustomobject]@{opcode=$op.Name;type=$resolved.ToString()}
 }
 $offset += $size
}
$constrained=@($typed|Where-Object {$_.opcode -eq 'constrained.' -and $_.type -eq 'TArguments'})
$boxed=@($typed|Where-Object {$_.opcode -eq 'box' -and $_.type -eq 'TArguments'})
if($constrained.Count -ne 2 -or $boxed.Count -ne 0){throw 'Unexpected argument dispatch IL'}
[pscustomobject]@{assemblySha256=(Get-FileHash -LiteralPath $path).Hash;method=$method.ToString();stateMachine=$state.ToString();argumentFields=@($state.GetFields([Reflection.BindingFlags]'Instance,Public,NonPublic')|Where-Object FieldType -eq $state.GetGenericArguments()[0]|ForEach-Object Name);constrainedArgumentCalls=$constrained.Count;boxedArguments=$boxed.Count;typedInstructions=$typed}|ConvertTo-Json -Depth 6
