			.method assembly hidebysig 
				instance object '<.cctor>b__81_7' (
					object receiver,
					valuetype Lokad.Lython.Runtime.LythonRuntime/ReceiverArguments arguments,
					class Lokad.Lython.LythonSourceSpan span,
					class Lokad.Lython.Runtime.LythonRuntime/ExecutionContext context
				) cil managed 
			{
				// Method begins at RVA 0x254a04
				// Header size: 12
				// Code size: 61 (0x3d)
				.maxstack 5
				.locals init (
					[0] valuetype [System.Runtime]System.Threading.Tasks.ValueTask`1<int32>,
					[1] valuetype [System.Runtime]System.Runtime.CompilerServices.ValueTaskAwaiter`1<int32>
				)

				IL_0000: ldarg.1
				IL_0001: castclass Lokad.Lython.Runtime.Text.PyString
				IL_0006: ldarga.s arguments
				IL_0008: call instance int32 Lokad.Lython.Runtime.LythonRuntime/ReceiverArguments::get_Length()
				IL_000d: ldc.i4.1
				IL_000e: beq.s IL_0013

				IL_0010: ldc.i4.8
				IL_0011: br.s IL_0034

				IL_0013: ldarga.s arguments
				IL_0015: ldc.i4.0
				IL_0016: call instance object Lokad.Lython.Runtime.LythonRuntime/ReceiverArguments::get_Item(int32)
				IL_001b: ldarg.s context
				IL_001d: ldarg.3
				IL_001e: ldc.i4.0
				IL_001f: call valuetype [System.Runtime]System.Threading.Tasks.ValueTask`1<int32> Lokad.Lython.Runtime.RuntimeArgumentValidation::CoerceIndexInt32Async(object, class Lokad.Lython.Runtime.LythonRuntime/ExecutionContext, class Lokad.Lython.LythonSourceSpan, bool)
				IL_0024: stloc.0
				IL_0025: ldloca.s 0
				IL_0027: call instance valuetype [System.Runtime]System.Runtime.CompilerServices.ValueTaskAwaiter`1<!0> valuetype [System.Runtime]System.Threading.Tasks.ValueTask`1<int32>::GetAwaiter()
				IL_002c: stloc.1
				IL_002d: ldloca.s 1
				IL_002f: call instance !0 valuetype [System.Runtime]System.Runtime.CompilerServices.ValueTaskAwaiter`1<int32>::GetResult()

				IL_0034: ldarg.s context
				IL_0036: ldarg.3
				IL_0037: call class Lokad.Lython.Runtime.Text.PyString Lokad.Lython.Runtime.Text.PyStringOps::ExpandTabs(class Lokad.Lython.Runtime.Text.PyString, int32, class Lokad.Lython.Runtime.LythonRuntime/ExecutionContext, class Lokad.Lython.LythonSourceSpan)
				IL_003c: ret
			} // end of method '<>c'::'<.cctor>b__81_7'
