/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.PythonTools.DkmDebugger.Proxies;
using Microsoft.PythonTools.DkmDebugger.Proxies.Structs;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.CallStack;
using Microsoft.VisualStudio.Debugger.Evaluation;

namespace Microsoft.PythonTools.DkmDebugger {
    internal class ExpressionEvaluator : DkmDataItem {
        // Layout of this struct must always remain in sync with DebuggerHelper/trace.cpp.
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        private struct ObjectToRelease {
            public ulong pyObject;
            public ulong next;
        }

        private const int MaxDebugChildren = 1000;

        private readonly DkmProcess _process;
        private readonly UInt64Proxy _objectsToRelease;

        public ExpressionEvaluator(DkmProcess process) {
            _process = process;
            var pyrtInfo = process.GetPythonRuntimeInfo();

            var py_DecRef = pyrtInfo.DLLs.DebuggerHelper.GetExportedStaticVariable<PointerProxy>("Py_DecRef");
            py_DecRef.Write(pyrtInfo.DLLs.Python.GetFunctionAddress("Py_DecRef"));

            _objectsToRelease = pyrtInfo.DLLs.DebuggerHelper.GetExportedStaticVariable<UInt64Proxy>("objectsToRelease");
        }

        private interface IPythonEvaluationResult {
            List<DkmEvaluationResult> GetChildren(ExpressionEvaluator exprEval, DkmEvaluationResult result, DkmInspectionContext inspectionContext);
        }

        private interface IPythonEvaluationResultAsync {
            void GetChildren(DkmEvaluationResult result, DkmWorkList workList, int initialRequestSize, DkmInspectionContext inspectionContext, DkmCompletionRoutine<DkmGetChildrenAsyncResult> completionRoutine);
        }

        private class RawEvaluationResult : DkmDataItem {
            public object Value { get; set; }
        }

        /// <summary>
        /// Data item attached to a <see cref="DkmEvaluationResult"/> that represents a Python object (a variable, field of another object, collection item etc).
        /// </summary>
        private class PyObjectEvaluationResult : DkmDataItem, IPythonEvaluationResult {
            public IValueStore<PyObject> ValueStore { get; set; }

            /// <summary>
            /// Does this object represent a [Python view] node?
            /// </summary>
            public bool IsPythonView { get; set; }

            /// <summary>
            /// Name of the C++ struct type corresponding to this object value.
            /// </summary>
            public string CppTypeName { get; set; }

            /// <summary>
            /// Name of the native module containing <see cref="CppTypeName"/>.
            /// </summary>
            public string CppTypeModuleName { get; set; }

            public List<DkmEvaluationResult> GetChildren(ExpressionEvaluator exprEval, DkmEvaluationResult result, DkmInspectionContext inspectionContext) {
                var stackFrame = result.StackFrame;
                var cppEval = new CppExpressionEvaluator(inspectionContext, stackFrame);
                var obj = ValueStore.Read();
                var evalResults = new List<DkmEvaluationResult>();
                var reprOptions = new ReprOptions { HexadecimalDisplay = (inspectionContext.Radix == 16) };
                var reprBuilder = new ReprBuilder(reprOptions);

                if (DebuggerOptions.ShowCppViewNodes && !IsPythonView) {
                    if (CppTypeName == null) {
                        // Try to guess the object's C++ type by looking at function pointers in its PyTypeObject. If they are pointing
                        // into a module for which symbols are available, C++ EE should be able to resolve them into something like
                        // "0x1e120d50 {python33_d.dll!list_dealloc(PyListObject *)}". If we are lucky, one of those functions will have
                        // the first argument declared as a strongly typed pointer, rather than PyObject* or void*.
                        CppTypeName = "PyObject";
                        CppTypeModuleName = null;
                        foreach (string methodField in _methodFields) {
                            var funcPtrEvalResult = cppEval.TryEvaluateObject(null, "PyObject", obj.Address, ".ob_type->" + methodField) as DkmSuccessEvaluationResult;
                            if (funcPtrEvalResult == null || funcPtrEvalResult.Value.IndexOf('{') < 0) {
                                continue;
                            }

                            var match = _cppFirstArgTypeFromFuncPtrRegex.Match(funcPtrEvalResult.Value);
                            string module = match.Groups["module"].Value;
                            string firstArgType = match.Groups["type"].Value;
                            if (firstArgType != "void" && firstArgType != "PyObject" && firstArgType != "_object") {
                                CppTypeName = firstArgType;
                                CppTypeModuleName = module;
                                break;
                            }
                        }
                    }

                    var cppEvalResult = cppEval.TryEvaluateObject(CppTypeModuleName, CppTypeName, obj.Address, ",!") as DkmSuccessEvaluationResult;
                    if (cppEvalResult != null) {
                        var evalResult = DkmSuccessEvaluationResult.Create(
                            inspectionContext, stackFrame, "[C++ view]", null,
                            DkmEvaluationResultFlags.ReadOnly | DkmEvaluationResultFlags.Expandable,
                            cppEvalResult.Value, null, cppEvalResult.Type,
                            DkmEvaluationResultCategory.Property,
                            DkmEvaluationResultAccessType.Private,
                            DkmEvaluationResultStorageType.None,
                            DkmEvaluationResultTypeModifierFlags.None,
                            null, null, null,
                            new CppViewEvaluationResult { CppEvaluationResult = cppEvalResult });
                        evalResults.Add(evalResult);
                    }
                }

                int i = 0;
                foreach (var child in obj.GetDebugChildren(reprOptions).Take(MaxDebugChildren)) {
                    if (child.Name == null) {
                        reprBuilder.Clear();
                        reprBuilder.AppendFormat("[{0:PY}]", i++);
                        child.Name = reprBuilder.ToString();
                    }

                    DkmEvaluationResult evalResult;
                    if (child.ValueStore is IValueStore<PyObject>) {
                        evalResult = exprEval.CreatePyObjectEvaluationResult(inspectionContext, stackFrame, child, cppEval);
                    } else {
                        var value = child.ValueStore.Read();
                        reprBuilder.Clear();
                        reprBuilder.AppendLiteral(value);

                        var flags = DkmEvaluationResultFlags.ReadOnly;
                        if (value is string) {
                            flags |= DkmEvaluationResultFlags.RawString;
                        }

                        evalResult = DkmSuccessEvaluationResult.Create(inspectionContext, stackFrame, child.Name, null, flags, reprBuilder.ToString(),
                            null, null, child.Category, child.AccessType, child.StorageType, child.TypeModifierFlags, null, null, null,
                            new RawEvaluationResult { Value = value });
                    }

                    evalResults.Add(evalResult);
                }

                return evalResults;
            }
        }

        /// <summary>
        /// Data item attached to the <see cref="DkmEvaluationResult"/> representing the [Globals] node.
        /// </summary>
        private class GlobalsEvaluationResult : DkmDataItem, IPythonEvaluationResult {
            public PyDictObject Globals { get; set; }

            public List<DkmEvaluationResult> GetChildren(ExpressionEvaluator exprEval, DkmEvaluationResult result, DkmInspectionContext inspectionContext) {
                var stackFrame = result.StackFrame;
                var cppEval = new CppExpressionEvaluator(inspectionContext, stackFrame);
                var evalResults = new List<DkmEvaluationResult>();

                foreach (var pair in Globals.ReadElements()) {
                    var name = pair.Key as IPyBaseStringObject;
                    if (name == null) {
                        continue;
                    }

                    var evalResult = exprEval.CreatePyObjectEvaluationResult(inspectionContext, stackFrame, new PythonEvaluationResult(pair.Value, name.ToString()), cppEval);
                    evalResults.Add(evalResult);
                }

                return evalResults.OrderBy(er => er.Name).ToList();
            }
        }

        /// <summary>
        /// Data item attached to the <see cref="DkmEvaluationResult"/> representing the [C++ view] node.
        /// </summary>
        private class CppViewEvaluationResult : DkmDataItem, IPythonEvaluationResultAsync {
            public DkmSuccessEvaluationResult CppEvaluationResult { get; set; }

            public void GetChildren(DkmEvaluationResult result, DkmWorkList workList, int initialRequestSize, DkmInspectionContext inspectionContext, DkmCompletionRoutine<DkmGetChildrenAsyncResult> completionRoutine) {
                CppEvaluationResult.GetChildren(workList, initialRequestSize, CppEvaluationResult.InspectionContext, (cppResult) => {
                    completionRoutine(cppResult);
                });
            }
        }

        private class EvaluationResults : DkmDataItem {
            public IEnumerable<DkmEvaluationResult> Results { get; set; }
        }

        // Names of fields of PyTypeObject struct that contain function pointers corresponding to standard methods of the type.
        // These are in rough descending order of probability of being non-null and strongly typed (so that we don't waste time eval'ing unnecessary).                                                              
        private static readonly string[] _methodFields = {
            "tp_init",
            "tp_dealloc",
            "tp_repr",
            "tp_hash",
            "tp_str",
            "tp_call",
            "tp_iter",
            "tp_iternext",
            "tp_richcompare",
            "tp_print",
            "tp_del",
            "tp_clear",
            "tp_traverse",
            "tp_getattr",
            "tp_setattr",
            "tp_getattro",
            "tp_setattro",
        };

        // Given something like "0x1e120d50 {python33_d.dll!list_dealloc(PyListObject *)}", extract "python33_d.dll" and "PyListObject".
        private static readonly Regex _cppFirstArgTypeFromFuncPtrRegex = new Regex(@"^.*?\{(?<module>.*?)\!.*?\((?<type>[0-9A-Za-z_:]*?)\s*\*(,.*?)?\)\}$", RegexOptions.CultureInvariant);

        /// <summary>
        /// Create a DkmEvaluationResult representing a Python object.
        /// </summary>
        /// <param name="cppEval">C++ evaluator to use to provide the [C++ view] node for this object.</param>
        /// <param name="cppTypeName">
        /// C++ struct name corresponding to this object type, for use by [C++ view] node. If not specified, it will be inferred from values of 
        /// various function pointers in <c>ob_type</c>, if possible. <c>PyObject</c> is the ultimate fallback.
        /// </param>
        public DkmEvaluationResult CreatePyObjectEvaluationResult(DkmInspectionContext inspectionContext, DkmStackWalkFrame stackFrame, PythonEvaluationResult pyEvalResult, CppExpressionEvaluator cppEval, string cppTypeName = null, bool isPythonView = false) {
            var name = pyEvalResult.Name;
            var valueStore = pyEvalResult.ValueStore as IValueStore<PyObject>;
            if (valueStore == null) {
                Debug.Fail("Non-PyObject PythonEvaluationResult passed to CreateEvaluationResult.");
                throw new ArgumentException();
            }

            var valueObj = valueStore.Read();
            string typeName = valueObj.ob_type.Read().tp_name.Read().Read();

            var reprOptions = new ReprOptions { HexadecimalDisplay = (inspectionContext.Radix == 16) };
            string repr = valueObj.Repr(reprOptions);

            var flags = DkmEvaluationResultFlags.None;
            if (DebuggerOptions.ShowCppViewNodes || valueObj.GetDebugChildren(reprOptions).Any()) {
                flags |= DkmEvaluationResultFlags.Expandable;
            }
            if (!(valueStore is IWritableDataProxy)) {
                flags |= DkmEvaluationResultFlags.ReadOnly;
            }
            if (valueObj is IPyBaseStringObject) {
                flags |= DkmEvaluationResultFlags.RawString;
            }

            return DkmSuccessEvaluationResult.Create(inspectionContext, stackFrame, name, null, flags, repr, null, typeName, pyEvalResult.Category,
                pyEvalResult.AccessType, pyEvalResult.StorageType, pyEvalResult.TypeModifierFlags, null, null, null,
                new PyObjectEvaluationResult { ValueStore = valueStore, CppTypeName = cppTypeName, IsPythonView = isPythonView });
        }

        public void GetFrameLocals(DkmInspectionContext inspectionContext, DkmWorkList workList, DkmStackWalkFrame stackFrame, DkmCompletionRoutine<DkmGetFrameLocalsAsyncResult> completionRoutine) {
            var pythonFrame = PyFrameObject.TryCreate(stackFrame);
            if (pythonFrame == null) {
                Debug.Fail("Non-Python frame passed to GetFrameLocals.");
                throw new NotSupportedException();
            }

            var cppEval = new CppExpressionEvaluator(inspectionContext, stackFrame);
            var evalResults = new List<DkmEvaluationResult>();

            var f_code = pythonFrame.f_code.Read();
            var f_localsplus = pythonFrame.f_localsplus;

            // Process cellvars and freevars first, because function arguments can appear in both cellvars and varnames if the argument is captured by a closure,
            // in which case we want to use the cellvar because the regular var slot will then be unused by Python (and in Python 3.4+, nulled out).
            var namesSeen = new HashSet<string>();
            var cellNames = f_code.co_cellvars.Read().ReadElements().Concat(f_code.co_freevars.Read().ReadElements());
            var cellSlots = f_localsplus.Skip(f_code.co_nlocals.Read());
            foreach (var pair in cellNames.Zip(cellSlots, (nameObj, cellSlot) => new { nameObj, cellSlot = cellSlot })) {
                var nameObj = pair.nameObj;
                var cellSlot = pair.cellSlot;

                var name = (nameObj.Read() as IPyBaseStringObject).ToStringOrNull();
                if (name == null) {
                    continue;
                }
                namesSeen.Add(name);

                if (cellSlot.IsNull) {
                    continue;
                }

                var cell = cellSlot.Read() as PyCellObject;
                if (cell == null) {
                    continue;
                }

                var localPtr = cell.ob_ref;
                if (localPtr.IsNull) {
                    continue;
                }

                var evalResult = CreatePyObjectEvaluationResult(inspectionContext, stackFrame, new PythonEvaluationResult(localPtr, name), cppEval);
                evalResults.Add(evalResult);
            }

            PyTupleObject co_varnames = f_code.co_varnames.Read();
            foreach (var pair in co_varnames.ReadElements().Zip(f_localsplus, (nameObj, varSlot) => new { nameObj, cellSlot = varSlot })) {
                var nameObj = pair.nameObj;
                var varSlot = pair.cellSlot;

                var name = (nameObj.Read() as IPyBaseStringObject).ToStringOrNull();
                if (name == null) {
                    continue;
                }

                // Check for function argument that was promoted to a cell.
                if (!namesSeen.Add(name)) {
                    continue;
                }

                if (varSlot.IsNull) {
                    continue;
                }

                var evalResult = CreatePyObjectEvaluationResult(inspectionContext, stackFrame, new PythonEvaluationResult(varSlot, name), cppEval);
                evalResults.Add(evalResult);
            }

            var globals = pythonFrame.f_globals.TryRead();
            if (globals != null) {
                var globalsEvalResult = new GlobalsEvaluationResult { Globals = globals };
                DkmEvaluationResult evalResult = DkmSuccessEvaluationResult.Create(
                    inspectionContext, stackFrame, "[Globals]", null,
                    DkmEvaluationResultFlags.ReadOnly | DkmEvaluationResultFlags.Expandable,
                    null, null, null,
                    DkmEvaluationResultCategory.Property,
                    DkmEvaluationResultAccessType.None,
                    DkmEvaluationResultStorageType.None,
                    DkmEvaluationResultTypeModifierFlags.None,
                    null, null, null, globalsEvalResult);

                // If it is a top-level module frame, show globals inline; otherwise, show them under the [Globals] node.
                if (f_code.co_name.Read().ToStringOrNull() == "<module>") {
                    evalResults.AddRange(globalsEvalResult.GetChildren(this, evalResult, inspectionContext));
                } else {
                    evalResults.Add(evalResult);

                    // Show any globals that are directly referenced by the function inline even in local frames.
                    var globalVars = (from pair in globals.ReadElements()
                                      let nameObj = pair.Key as IPyBaseStringObject
                                      where nameObj != null
                                      select new { Name = nameObj.ToString(), Value = pair.Value }
                                     ).ToLookup(v => v.Name, v => v.Value);

                    PyTupleObject co_names = f_code.co_names.Read();
                    foreach (var nameObj in co_names.ReadElements()) {
                        var name = (nameObj.Read() as IPyBaseStringObject).ToStringOrNull();
                        if (name == null) {
                            continue;
                        }

                        // If this is a used name but it was not in varnames or freevars, it is a directly referenced global.
                        if (!namesSeen.Add(name)) {
                            continue;
                        }

                        var varSlot = globalVars[name].FirstOrDefault();
                        if (varSlot.Process != null) {
                            evalResult = CreatePyObjectEvaluationResult(inspectionContext, stackFrame, new PythonEvaluationResult(varSlot, name), cppEval);
                            evalResults.Add(evalResult);
                        }
                    }
                }
            }

            var enumContext = DkmEvaluationResultEnumContext.Create(evalResults.Count, stackFrame, inspectionContext,
                new EvaluationResults { Results = evalResults.OrderBy(er => er.Name) });
            completionRoutine(new DkmGetFrameLocalsAsyncResult(enumContext));
        }

        public void GetChildren(DkmEvaluationResult result, DkmWorkList workList, int initialRequestSize, DkmInspectionContext inspectionContext, DkmCompletionRoutine<DkmGetChildrenAsyncResult> completionRoutine) {
            var asyncEvalResult = result.GetDataItem<CppViewEvaluationResult>();
            if (asyncEvalResult != null) {
                asyncEvalResult.GetChildren(result, workList, initialRequestSize, inspectionContext, completionRoutine);
                return;
            }

            var pyEvalResult =
                (IPythonEvaluationResult)result.GetDataItem<PyObjectEvaluationResult>() ??
                (IPythonEvaluationResult)result.GetDataItem<GlobalsEvaluationResult>();
            if (pyEvalResult != null) {
                var childResults = pyEvalResult.GetChildren(this, result, inspectionContext);
                completionRoutine(
                    new DkmGetChildrenAsyncResult(
                        new DkmEvaluationResult[0],
                        DkmEvaluationResultEnumContext.Create(
                            childResults.Count,
                            result.StackFrame,
                            inspectionContext,
                            new EvaluationResults { Results = childResults.ToArray() })));
                return;
            }

            Debug.Fail("GetChildren called on an unsupported DkmEvaluationResult.");
            throw new NotSupportedException();
        }

        public void GetItems(DkmEvaluationResultEnumContext enumContext, DkmWorkList workList, int startIndex, int count, DkmCompletionRoutine<DkmEvaluationEnumAsyncResult> completionRoutine) {
            var evalResults = enumContext.GetDataItem<EvaluationResults>();
            if (evalResults == null) {
                Debug.Fail("GetItems called on a DkmEvaluationResultEnumContext without an associated EvaluationResults.");
                throw new NotSupportedException();
            }

            var result = evalResults.Results.Skip(startIndex).Take(count).ToArray();
            completionRoutine(new DkmEvaluationEnumAsyncResult(result));
        }

        public string GetUnderlyingString(DkmEvaluationResult result) {
            var rawResult = result.GetDataItem<RawEvaluationResult>();
            if (rawResult != null && rawResult.Value is string) {
                return (string)rawResult.Value;
            }

            var objResult = result.GetDataItem<PyObjectEvaluationResult>();
            if (objResult == null) {
                return null;
            }

            var str = objResult.ValueStore.Read() as IPyBaseStringObject;
            return str.ToStringOrNull();
        }

        private class StringErrorSink : ErrorSink {
            private readonly StringBuilder _builder = new StringBuilder();

            public override void Add(string message, int[] lineLocations, int startIndex, int endIndex, int errorCode, Severity severity) {
                _builder.AppendLine(message);
            }

            public override string ToString() {
                return _builder.ToString();
            }
        }

        public unsafe void SetValueAsString(DkmEvaluationResult result, string value, int timeout, out string errorText) {
            var pyEvalResult = result.GetDataItem<PyObjectEvaluationResult>();
            if (pyEvalResult == null) {
                Debug.Fail("SetValueAsString called on a DkmEvaluationResult without an associated PyObjectEvaluationResult.");
                throw new NotSupportedException();
            }

            var proxy = pyEvalResult.ValueStore as IWritableDataProxy;
            if (proxy == null) {
                Debug.Fail("SetValueAsString called on a DkmEvaluationResult that does not correspond to an IWritableDataProxy.");
                throw new InvalidOperationException();
            }

            errorText = null;
            var process = result.StackFrame.Process;
            var pyrtInfo = process.GetPythonRuntimeInfo();

            var parserOptions = new ParserOptions { ErrorSink = new StringErrorSink() };
            var parser = Parser.CreateParser(new StringReader(value), pyrtInfo.LanguageVersion, parserOptions);
            var body = (ReturnStatement)parser.ParseTopExpression().Body;
            errorText = parserOptions.ErrorSink.ToString();
            if (!string.IsNullOrEmpty(errorText)) {
                return;
            }

            var expr = body.Expression;
            while (true) {
                var parenExpr = expr as ParenthesisExpression;
                if (parenExpr == null) {
                    break;
                }
                expr = parenExpr.Expression;
            }

            int sign;
            expr = ForceExplicitSign(expr, out sign);

            PyObject newObj = null;

            var constExpr = expr as ConstantExpression;
            if (constExpr != null) {
                if (constExpr.Value == null) {
                    newObj = PyObject.None(process);
                } else if (constExpr.Value is bool) {
                    // In 2.7, 'True' and 'False' are reported as identifiers, not literals, and are handled separately below.
                    newObj = PyBoolObject33.Create(process, (bool)constExpr.Value);
                } else if (constExpr.Value is string) {
                    if (pyrtInfo.LanguageVersion <= PythonLanguageVersion.V27) {
                        newObj = PyUnicodeObject27.Create(process, (string)constExpr.Value);
                    } else {
                        newObj = PyUnicodeObject33.Create(process, (string)constExpr.Value);
                    }
                } else if (constExpr.Value is AsciiString) {
                    newObj = PyBytesObject.Create(process, (AsciiString)constExpr.Value);
                }
            } else {
                var unaryExpr = expr as UnaryExpression;
                if (unaryExpr != null && sign != 0) {
                    constExpr = unaryExpr.Expression as ConstantExpression;
                    if (constExpr != null) {
                        if (constExpr.Value is BigInteger) {
                            newObj = PyLongObject.Create(process, (BigInteger)constExpr.Value * sign);
                        } else if (constExpr.Value is int) {
                            if (pyrtInfo.LanguageVersion <= PythonLanguageVersion.V27) {
                                newObj = PyIntObject.Create(process, (int)constExpr.Value * sign);
                            } else {
                                newObj = PyLongObject.Create(process, (int)constExpr.Value * sign);
                            }
                        } else if (constExpr.Value is double) {
                            newObj = PyFloatObject.Create(process, (double)constExpr.Value * sign);
                        } else if (constExpr.Value is Complex) {
                            newObj = PyComplexObject.Create(process, (Complex)constExpr.Value * sign);
                        }
                    }
                } else {
                    var binExpr = expr as BinaryExpression;
                    if (binExpr != null && (binExpr.Operator == PythonOperator.Add || binExpr.Operator == PythonOperator.Subtract)) {
                        int realSign;
                        var realExpr = ForceExplicitSign(binExpr.Left, out realSign) as UnaryExpression;
                        int imagSign;
                        var imagExpr = ForceExplicitSign(binExpr.Right, out imagSign) as UnaryExpression;
                        if (realExpr != null && realSign != 0 && imagExpr != null && imagSign != 0) {
                            var realConst = realExpr.Expression as ConstantExpression;
                            var imagConst = imagExpr.Expression as ConstantExpression;
                            if (realConst != null && imagConst != null) {
                                var realVal = (realConst.Value as int? ?? realConst.Value as double?) as IConvertible;
                                var imagVal = imagConst.Value as Complex?;
                                if (realVal != null && imagVal != null) {
                                    double real = realVal.ToDouble(null) * realSign;
                                    double imag = imagVal.Value.Imaginary * imagSign * (binExpr.Operator == PythonOperator.Add ? 1 : -1);
                                    newObj = PyComplexObject.Create(process, new Complex(real, imag));
                                }
                            }
                        }
                    } else {
                        if (pyrtInfo.LanguageVersion <= PythonLanguageVersion.V27) {
                            // 'True' and 'False' are not literals in 2.x, but we want to treat them as such.
                            var name = expr as NameExpression;
                            if (name.Name == "True") {
                                newObj = PyBoolObject27.Create(process, true);
                            } else if (name.Name == "False") {
                                newObj = PyBoolObject27.Create(process, false);
                            }
                        }
                    }
                }
            }

            if (newObj != null) {
                var oldObj = proxy.Read() as PyObject;
                if (oldObj != null) {
                    // We can't free the original value without running some code in the process, and it may be holding heap locks.
                    // So don't decrement refcount now, but instead add it to the list of objects for TraceFunc to GC when it gets
                    // a chance to run next time.

                    byte[] buf = new byte[sizeof(ObjectToRelease)];
                    fixed (byte* p = buf) {
                        var otr = (ObjectToRelease*)p;
                        otr->pyObject = oldObj.Address;
                        otr->next = _objectsToRelease.Read();
                    }

                    ulong otrPtr = _process.AllocateVirtualMemory(0, sizeof(ObjectToRelease), NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE, NativeMethods.PAGE_READWRITE);
                    _process.WriteMemory(otrPtr, buf);
                    _objectsToRelease.Write(otrPtr);
                }

                newObj.ob_refcnt.Increment();
                proxy.Write(newObj);
            } else {
                errorText = "Only boolean, numeric or string literals and None are supported.";
            }
        }

        private static Expression ForceExplicitSign(Expression expr, out int sign) {
            var constExpr = expr as ConstantExpression;
            if (constExpr != null && (constExpr.Value is int || constExpr.Value is double || constExpr.Value is BigInteger || constExpr.Value is Complex)) {
                sign = 1;
                return new UnaryExpression(PythonOperator.Pos, constExpr);
            }

            var unaryExpr = expr as UnaryExpression;
            if (unaryExpr != null) {
                switch (unaryExpr.Op) {
                    case PythonOperator.Pos:
                        sign = 1;
                        return unaryExpr;
                    case PythonOperator.Negate:
                        sign = -1;
                        return unaryExpr;
                }
            }

            sign = 0;
            return expr;
        }
    }

    internal class PythonEvaluationResult {
        /// <summary>
        /// A store containing the evaluated value.
        /// </summary>
        public IValueStore ValueStore { get; set; }

        /// <summary>
        /// For named results, name of the result. For unnamed results, <c>null</c>.
        /// </summary>
        public string Name { get; set; }

        public DkmEvaluationResultCategory Category { get; set; }

        public DkmEvaluationResultAccessType AccessType { get; set; }

        public DkmEvaluationResultStorageType StorageType { get; set; }

        public DkmEvaluationResultTypeModifierFlags TypeModifierFlags { get; set; }

        public PythonEvaluationResult(IValueStore valueStore, string name = null) {
            ValueStore = valueStore;
            Name = name;
            Category = DkmEvaluationResultCategory.Data;
        }
    }
}
