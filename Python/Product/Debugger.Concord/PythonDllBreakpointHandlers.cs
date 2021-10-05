// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using Microsoft.PythonTools.Debugger.Concord.Proxies.Structs;

namespace Microsoft.PythonTools.Debugger.Concord
{
	internal partial class TraceManagerLocalHelper
	{
		private class PythonDllBreakpointHandlers
		{
			private readonly TraceManagerLocalHelper _owner;

			public PythonDllBreakpointHandlers(TraceManagerLocalHelper owner)
			{
				_owner = owner;
			}

			public void new_threadstate(DkmThread thread, ulong frameBase, ulong vframe, ulong returnAddress)
			{
				var process = thread.Process;
				var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

				// Addressing this local by name does not work for release builds, so read the return value directly from the register instead.
				var tstate = PyThreadState.TryCreate(process, cppEval.EvaluateReturnValueUInt64());
				if (tstate == null)
				{
					return;
				}

				_owner.RegisterTracing(tstate);
			}

			public void PyInterpreterState_New(DkmThread thread, ulong frameBase, ulong vframe, ulong returnAddress)
			{
				var process = thread.Process;
				var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

				var istate = PyInterpreterState.TryCreate(process, cppEval.EvaluateReturnValueUInt64());
				if (istate == null)
				{
					return;
				}

				if (process.GetPythonRuntimeInfo().LanguageVersion >= PythonLanguageVersion.V36)
				{
					_owner.RegisterJITTracing(istate);
				}
			}

			// This step-in gate is not marked [StepInGate] because it doesn't live in pythonXX.dll, and so we register it manually.
			public void _call_function_pointer(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters)
			{
				var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);
				ulong pProc = cppEval.EvaluateUInt64(useRegisters ? "@rdx" : "pProc");
				_owner.OnPotentialRuntimeExit(thread, pProc);
			}

			[StepInGate]
			public void call_function(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters)
			{
				var process = thread.Process;
				var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

				int oparg = cppEval.EvaluateInt32(useRegisters ? "@rdx" : "oparg");

				int na = oparg & 0xff;
				int nk = (oparg >> 8) & 0xff;
				int n = na + 2 * nk;

				ulong func = cppEval.EvaluateUInt64(
					"*((*(PyObject***){0}) - {1} - 1)",
					useRegisters ? "@rcx" : "pp_stack",
					n);
				var obj = PyObject.FromAddress(process, func);
				ulong ml_meth = cppEval.EvaluateUInt64(
					"((PyObject*){0})->ob_type == &PyCFunction_Type ? ((PyCFunctionObject*){0})->m_ml->ml_meth : 0",
					func);

				_owner.OnPotentialRuntimeExit(thread, ml_meth);
			}

			[StepInGate]
			public void PyCFunction_Call(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters)
			{
				var process = thread.Process;
				var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

				ulong ml_meth = cppEval.EvaluateUInt64(
					"((PyObject*){0})->ob_type == &PyCFunction_Type ? ((PyCFunctionObject*){0})->m_ml->ml_meth : 0",
					useRegisters ? "@rcx" : "func");
				_owner.OnPotentialRuntimeExit(thread, ml_meth);
			}

			[StepInGate]
			public void getset_get(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters)
			{
				var process = thread.Process;
				var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

				string descrVar = useRegisters ? "((PyGetSetDescrObject*)@rcx)" : "descr";

				ulong get = cppEval.EvaluateUInt64(descrVar + "->d_getset->get");
				_owner.OnPotentialRuntimeExit(thread, get);
			}

			[StepInGate]
			public void getset_set(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters)
			{
				var process = thread.Process;
				var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

				string descrVar = useRegisters ? "((PyGetSetDescrObject*)@rcx)" : "descr";

				ulong set = cppEval.EvaluateUInt64(descrVar + "->d_getset->set");
				_owner.OnPotentialRuntimeExit(thread, set);
			}

			[StepInGate(HasMultipleExitPoints = true)]
			public void type_call(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters)
			{
				var process = thread.Process;
				var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

				string typeVar = useRegisters ? "((PyTypeObject*)@rcx)" : "type";

				ulong tp_new = cppEval.EvaluateUInt64(typeVar + "->tp_new");
				_owner.OnPotentialRuntimeExit(thread, tp_new);

				ulong tp_init = cppEval.EvaluateUInt64(typeVar + "->tp_init");
				_owner.OnPotentialRuntimeExit(thread, tp_init);
			}

			[StepInGate]
			public void PyType_GenericNew(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters)
			{
				var process = thread.Process;
				var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

				string typeVar = useRegisters ? "((PyTypeObject*)@rcx)" : "type";

				ulong tp_alloc = cppEval.EvaluateUInt64(typeVar + "->tp_alloc");
				_owner.OnPotentialRuntimeExit(thread, tp_alloc);
			}

			[StepInGate]
			public void PyObject_Print(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters)
			{
				var process = thread.Process;
				var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

				string opVar = useRegisters ? "((PyObject*)@rcx)" : "op";

				ulong tp_print = cppEval.EvaluateUInt64(opVar + "->ob_type->tp_print");
				_owner.OnPotentialRuntimeExit(thread, tp_print);
			}

			[StepInGate]
			public void PyObject_GetAttrString(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters)
			{
				var process = thread.Process;
				var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

				string vVar = useRegisters ? "((PyObject*)@rcx)" : "v";

				ulong tp_getattr = cppEval.EvaluateUInt64(vVar + "->ob_type->tp_getattr");
				_owner.OnPotentialRuntimeExit(thread, tp_getattr);
			}

			[StepInGate]
			public void PyObject_SetAttrString(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters)
			{
				var process = thread.Process;
				var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

				string vVar = useRegisters ? "((PyObject*)@rcx)" : "v";

				ulong tp_setattr = cppEval.EvaluateUInt64(vVar + "->ob_type->tp_setattr");
				_owner.OnPotentialRuntimeExit(thread, tp_setattr);
			}

			[StepInGate]
			public void PyObject_GetAttr(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters)
			{
				var process = thread.Process;
				var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

				string vVar = useRegisters ? "((PyObject*)@rcx)" : "v";

				ulong tp_getattr = cppEval.EvaluateUInt64(vVar + "->ob_type->tp_getattr");
				_owner.OnPotentialRuntimeExit(thread, tp_getattr);

				ulong tp_getattro = cppEval.EvaluateUInt64(vVar + "->ob_type->tp_getattro");
				_owner.OnPotentialRuntimeExit(thread, tp_getattro);
			}

			[StepInGate]
			public void PyObject_SetAttr(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters)
			{
				var process = thread.Process;
				var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

				string vVar = useRegisters ? "((PyObject*)@rcx)" : "v";

				ulong tp_setattr = cppEval.EvaluateUInt64(vVar + "->ob_type->tp_setattr");
				_owner.OnPotentialRuntimeExit(thread, tp_setattr);

				ulong tp_setattro = cppEval.EvaluateUInt64(vVar + "->ob_type->tp_setattro");
				_owner.OnPotentialRuntimeExit(thread, tp_setattro);
			}

			[StepInGate]
			public void PyObject_Repr(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters)
			{
				var process = thread.Process;
				var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

				string vVar = useRegisters ? "((PyObject*)@rcx)" : "v";

				ulong tp_repr = cppEval.EvaluateUInt64(vVar + "->ob_type->tp_repr");
				_owner.OnPotentialRuntimeExit(thread, tp_repr);
			}

			[StepInGate]
			public void PyObject_Hash(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters)
			{
				var process = thread.Process;
				var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

				string vVar = useRegisters ? "((PyObject*)@rcx)" : "v";

				ulong tp_hash = cppEval.EvaluateUInt64(vVar + "->ob_type->tp_hash");
				_owner.OnPotentialRuntimeExit(thread, tp_hash);
			}

			[StepInGate]
			public void PyObject_Call(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters)
			{
				var process = thread.Process;
				var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

				string funcVar = useRegisters ? "((PyObject*)@rcx)" : "func";

				ulong tp_call = cppEval.EvaluateUInt64(funcVar + "->ob_type->tp_call");
				_owner.OnPotentialRuntimeExit(thread, tp_call);
			}

			[StepInGate]
			public void PyObject_Str(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters)
			{
				var process = thread.Process;
				var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

				string vVar = useRegisters ? "((PyObject*)@rcx)" : "v";

				ulong tp_str = cppEval.EvaluateUInt64(vVar + "->ob_type->tp_str");
				_owner.OnPotentialRuntimeExit(thread, tp_str);
			}

			[StepInGate(MaxVersion = PythonLanguageVersion.V27, HasMultipleExitPoints = true)]
			public void do_cmp(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters)
			{
				var process = thread.Process;
				var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

				string vVar = useRegisters ? "((PyObject*)@rcx)" : "v";
				string wVar = useRegisters ? "((PyObject*)@rdx)" : "w";

				ulong tp_compare1 = cppEval.EvaluateUInt64(vVar + "->ob_type->tp_compare");
				_owner.OnPotentialRuntimeExit(thread, tp_compare1);

				ulong tp_richcompare1 = cppEval.EvaluateUInt64(vVar + "->ob_type->tp_richcompare");
				_owner.OnPotentialRuntimeExit(thread, tp_richcompare1);

				ulong tp_compare2 = cppEval.EvaluateUInt64(wVar + "->ob_type->tp_compare");
				_owner.OnPotentialRuntimeExit(thread, tp_compare2);

				ulong tp_richcompare2 = cppEval.EvaluateUInt64(wVar + "->ob_type->tp_richcompare");
				_owner.OnPotentialRuntimeExit(thread, tp_richcompare2);
			}

			[StepInGate(MaxVersion = PythonLanguageVersion.V27, HasMultipleExitPoints = true)]
			public void PyObject_RichCompare(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters)
			{
				var process = thread.Process;
				var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

				string vVar = useRegisters ? "((PyObject*)@rcx)" : "v";
				string wVar = useRegisters ? "((PyObject*)@rdx)" : "w";

				ulong tp_compare1 = cppEval.EvaluateUInt64(vVar + "->ob_type->tp_compare");
				_owner.OnPotentialRuntimeExit(thread, tp_compare1);

				ulong tp_richcompare1 = cppEval.EvaluateUInt64(vVar + "->ob_type->tp_richcompare");
				_owner.OnPotentialRuntimeExit(thread, tp_richcompare1);

				ulong tp_compare2 = cppEval.EvaluateUInt64(wVar + "->ob_type->tp_compare");
				_owner.OnPotentialRuntimeExit(thread, tp_compare2);

				ulong tp_richcompare2 = cppEval.EvaluateUInt64(wVar + "->ob_type->tp_richcompare");
				_owner.OnPotentialRuntimeExit(thread, tp_richcompare2);
			}

			[StepInGate(MinVersion = PythonLanguageVersion.V33, HasMultipleExitPoints = true)]
			public void do_richcompare(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters)
			{
				var process = thread.Process;
				var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

				string vVar = useRegisters ? "((PyObject*)@rcx)" : "v";
				string wVar = useRegisters ? "((PyObject*)@rdx)" : "w";

				ulong tp_richcompare1 = cppEval.EvaluateUInt64(vVar + "->ob_type->tp_richcompare");
				_owner.OnPotentialRuntimeExit(thread, tp_richcompare1);

				ulong tp_richcompare2 = cppEval.EvaluateUInt64(wVar + "->ob_type->tp_richcompare");
				_owner.OnPotentialRuntimeExit(thread, tp_richcompare2);
			}

			[StepInGate]
			public void PyObject_GetIter(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters)
			{
				var process = thread.Process;
				var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

				string oVar = useRegisters ? "((PyObject*)@rcx)" : "o";

				ulong tp_iter = cppEval.EvaluateUInt64(oVar + "->ob_type->tp_iter");
				_owner.OnPotentialRuntimeExit(thread, tp_iter);
			}

			[StepInGate]
			public void PyIter_Next(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters)
			{
				var process = thread.Process;
				var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

				string iterVar = useRegisters ? "((PyObject*)@rcx)" : "iter";

				ulong tp_iternext = cppEval.EvaluateUInt64(iterVar + "->ob_type->tp_iternext");
				_owner.OnPotentialRuntimeExit(thread, tp_iternext);
			}

			[StepInGate]
			public void builtin_next(DkmThread thread, ulong frameBase, ulong vframe, bool useRegisters)
			{
				var process = thread.Process;
				var cppEval = new CppExpressionEvaluator(thread, frameBase, vframe);

				string argsVar = useRegisters ? "((PyTupleObject*)@rdx)" : "((PyTupleObject*)args)";

				ulong tp_iternext = cppEval.EvaluateUInt64(argsVar + "->ob_item[0]->ob_type->tp_iternext");
				_owner.OnPotentialRuntimeExit(thread, tp_iternext);
			}
		}
	}
}
