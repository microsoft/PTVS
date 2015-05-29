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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Dia;
using Microsoft.PythonTools.DkmDebugger.Proxies.Structs;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Evaluation;
using Microsoft.VisualStudio.Debugger.Evaluation.IL;

namespace Microsoft.PythonTools.DkmDebugger {
    /// <summary>
    /// A custom natvis visualizer for PyObject that replaces it with the corresponding Python visualizer if Python runtime is loaded in the process.
    /// </summary>
    internal class PyObjectNativeVisualizer : DkmDataItem {
        private DkmEvaluationResult GetPythonView(DkmVisualizedExpression visualizedExpression) {
            var stackFrame = visualizedExpression.StackFrame;
            var process = stackFrame.Process;
            var pythonRuntime = process.GetPythonRuntimeInstance();
            if (pythonRuntime == null) {
                return null;
            }

            var home = visualizedExpression.ValueHome as DkmPointerValueHome;
            if (home == null) {
                Debug.Fail("PythonViewNativeVisualizer given a visualized expression that has a non-DkmPointerValueHome home.");
                return null;
            } else if (home.Address == 0) {
                return null;
            }

            var exprEval = process.GetDataItem<ExpressionEvaluator>();
            if (exprEval == null) {
                Debug.Fail("PythonViewNativeVisualizer failed to obtain an instance of ExpressionEvaluator.");
                return null;
            }

            string cppTypeName = null;
            var childExpr = visualizedExpression as DkmChildVisualizedExpression;
            if (childExpr != null) {
                var evalResult = childExpr.EvaluationResult as DkmSuccessEvaluationResult;
                cppTypeName = evalResult.Type;
            } else {
                object punkTypeSymbol;
                visualizedExpression.GetSymbolInterface(typeof(IDiaSymbol).GUID, out punkTypeSymbol);
                using (ComPtr.Create(punkTypeSymbol)) {
                    var typeSymbol = punkTypeSymbol as IDiaSymbol;
                    if (typeSymbol != null) {
                        cppTypeName = typeSymbol.name;
                    }
                }
            }

            PyObject objRef;
            try {
                objRef = PyObject.FromAddress(process, home.Address);
            } catch {
                return null;
            }

            var pyEvalResult = new PythonEvaluationResult(objRef, "[Python view]")
            {
                Category = DkmEvaluationResultCategory.Property,
                AccessType = DkmEvaluationResultAccessType.Private
            };

            var inspectionContext = visualizedExpression.InspectionContext;
            CppExpressionEvaluator cppEval;
            try {
                cppEval = new CppExpressionEvaluator(inspectionContext, stackFrame);
            } catch {
                return null;
            }

            var pythonContext = DkmInspectionContext.Create(visualizedExpression.InspectionSession, pythonRuntime, stackFrame.Thread,
                inspectionContext.Timeout, inspectionContext.EvaluationFlags, inspectionContext.FuncEvalFlags, inspectionContext.Radix,
                DkmLanguage.Create("Python", new DkmCompilerId(Guids.MicrosoftVendorGuid, Guids.PythonLanguageGuid)), null);
            try {
                return exprEval.CreatePyObjectEvaluationResult(pythonContext, stackFrame, null, pyEvalResult, cppEval, cppTypeName, hasCppView: true);
            } catch {
                return null;
            }
        }

#if DEV14_OR_LATER

        // In VS 2015+, the injection of the child [Python view] node is handled in the PythonDkm.natvis, and the visualizer is only responsible
        // for producing a DkmEvaluationResult for that node.

        public void EvaluateVisualizedExpression(DkmVisualizedExpression visualizedExpression, out DkmEvaluationResult resultObject) {
            resultObject = GetPythonView(visualizedExpression);
            if (resultObject == null) {
                resultObject = DkmFailedEvaluationResult.Create(
                    visualizedExpression.InspectionContext, visualizedExpression.StackFrame, "[Python view]",
                    null, "Python view is unavailable for this object", DkmEvaluationResultFlags.Invalid, null);
            }
        }

        public void GetChildren(DkmVisualizedExpression visualizedExpression, int initialRequestSize, DkmInspectionContext inspectionContext, out DkmChildVisualizedExpression[] initialChildren, out DkmEvaluationResultEnumContext enumContext) {
            throw new NotImplementedException();
        }

        public void GetItems(DkmVisualizedExpression visualizedExpression, DkmEvaluationResultEnumContext enumContext, int startIndex, int count, out DkmChildVisualizedExpression[] items) {
            throw new NotImplementedException();
        }

        public string GetUnderlyingString(DkmVisualizedExpression visualizedExpression) {
            throw new NotImplementedException();
        }

        public void SetValueAsString(DkmVisualizedExpression visualizedExpression, string value, int timeout, out string errorText) {
            throw new NotImplementedException();
        }

        public void UseDefaultEvaluationBehavior(DkmVisualizedExpression visualizedExpression, out bool useDefaultEvaluationBehavior, out DkmEvaluationResult defaultEvaluationResult) {
            useDefaultEvaluationBehavior = true;
            defaultEvaluationResult = GetPythonView(visualizedExpression);
            if (defaultEvaluationResult == null) {
                throw new NotSupportedException();
            }
        }

        public DkmILEvaluationResult[] Execute(DkmILExecuteIntrinsic executeIntrinsic, DkmILContext iLContext, DkmCompiledILInspectionQuery inspectionQuery, DkmILEvaluationResult[] arguments, ReadOnlyCollection<DkmCompiledInspectionQuery> subroutines, out DkmILFailureReason failureReason) {
            var pythonRuntime = iLContext.StackFrame.Process.GetPythonRuntimeInstance();

            // The mapping between functions and IDs is defined in PythonDkm.natvis.
            switch (executeIntrinsic.Id) {
                case 1: // PTVS_ShowPythonViewNodes
                    failureReason = DkmILFailureReason.None;
                    return new[] { DkmILEvaluationResult.Create(executeIntrinsic.SourceId, new ReadOnlyCollection<byte>(new byte[] {
                        pythonRuntime != null && DebuggerOptions.ShowPythonViewNodes ? (byte)1 : (byte)0
                    }))};

                default:
                    throw new NotSupportedException();
            }
        }

#else

        // VS 2013 does not support custom visualizers for <Synthetic> nodes in natvis, and so the visualizer is instead associated with the object itself,
        // and has to pass through all the children from the default C++ visualizer, and then inject the [Python view] node in addition to them.

        private class RawEvaluationResultHolder : DkmDataItem {
            public DkmEvaluationResult RawResult { get; set; }

            protected override void OnClose() {
                base.OnClose();
                RawResult.Close();
            }
        }

        private class RawEnumContextData : DkmDataItem {
            public DkmEvaluationResultEnumContext RawContext { get; set; }
            public DkmChildVisualizedExpression PythonView { get; set; }

            protected override void OnClose() {
                base.OnClose();
                RawContext.Close();
                PythonView.Close();
            }
        }

        public void EvaluateVisualizedExpression(DkmVisualizedExpression visualizedExpression, out DkmEvaluationResult resultObject) {
            var rootExpr = visualizedExpression as DkmRootVisualizedExpression;
            if (rootExpr == null) {
                Debug.Fail("PythonViewNativeVisualizer.EvaluateVisualizedExpression was given a visualized expression that is not a DkmRootVisualizedExpression.");
                throw new NotSupportedException();
            }

            DkmEvaluationResult rawResult;
            RawEvaluationResultHolder rawResultHolder;
            using (var rawExpr = DkmLanguageExpression.Create(CppExpressionEvaluator.CppLanguage, DkmEvaluationFlags.ShowValueRaw, rootExpr.FullName + ",!", null)) {
                rootExpr.EvaluateExpressionCallback(rootExpr.InspectionContext, rawExpr, rootExpr.StackFrame, out rawResult);
                rawResultHolder = new RawEvaluationResultHolder { RawResult = rawResult };
                rootExpr.SetDataItem(DkmDataCreationDisposition.CreateAlways, rawResultHolder);
            }

            var rawSuccessResult = rawResult as DkmSuccessEvaluationResult;
            if (rawSuccessResult != null) {
                resultObject = DkmSuccessEvaluationResult.Create(
                    rawResult.InspectionContext,
                    rawResult.StackFrame,
                    rootExpr.Name,
                    rawSuccessResult.FullName,
                    rawSuccessResult.Flags,
                    rawSuccessResult.Value,
                    rawSuccessResult.EditableValue,
                    rawSuccessResult.Type,
                    rawSuccessResult.Category,
                    rawSuccessResult.Access,
                    rawSuccessResult.StorageType,
                    rawSuccessResult.TypeModifierFlags,
                    rawSuccessResult.Address,
                    rawSuccessResult.CustomUIVisualizers,
                    rawSuccessResult.ExternalModules,
                    rawResultHolder);
                return;
            }

            var rawFailedResult = rawResult as DkmFailedEvaluationResult;
            if (rawFailedResult != null) {
                resultObject = DkmFailedEvaluationResult.Create(
                    rawResult.InspectionContext,
                    rawResult.StackFrame,
                    rootExpr.Name,
                    rootExpr.FullName,
                    rawFailedResult.ErrorMessage,
                    rawFailedResult.Flags,
                    rawResultHolder);
                return;
            }

            Debug.Fail("Raw evaluation result was neither DkmSuccessEvaluationResult nor DkmFailedEvaluationResult.");
            throw new NotSupportedException();
        }

        public void GetChildren(DkmVisualizedExpression visualizedExpression, int initialRequestSize, DkmInspectionContext inspectionContext, out DkmChildVisualizedExpression[] initialChildren, out DkmEvaluationResultEnumContext enumContext) {
            var rawResultHolder = visualizedExpression.GetDataItem<RawEvaluationResultHolder>();
            if (rawResultHolder == null) {
                Debug.Fail("PythonViewNativeVisualizer.GetChildren passed a visualized expression that does not have an associated RawEvaluationResultHolder.");
                throw new NotSupportedException();
            }
            var rawResult = rawResultHolder.RawResult;

            DkmEvaluationResult[] rawInitialChildren;
            DkmEvaluationResultEnumContext rawEnumContext;
            visualizedExpression.GetChildrenCallback(rawResult, 0, inspectionContext, out rawInitialChildren, out rawEnumContext);

            initialChildren = new DkmChildVisualizedExpression[0];
            enumContext = rawEnumContext;

            if (DebuggerOptions.ShowPythonViewNodes) {
                var pythonViewEvalResult = GetPythonView(visualizedExpression);
                var pythonView = DkmChildVisualizedExpression.Create(
                    visualizedExpression.InspectionContext,
                    visualizedExpression.VisualizerId,
                    visualizedExpression.SourceId,
                    visualizedExpression.StackFrame,
                    visualizedExpression.ValueHome,
                    pythonViewEvalResult,
                    visualizedExpression,
                    (uint)rawEnumContext.Count, null);
                if (pythonView != null) {
                    enumContext = DkmEvaluationResultEnumContext.Create(
                        rawEnumContext.Count + 1,
                        rawEnumContext.StackFrame,
                        rawEnumContext.InspectionContext,
                        new RawEnumContextData { RawContext = rawEnumContext, PythonView = pythonView });
                }
            }
        }

        public void GetItems(DkmVisualizedExpression visualizedExpression, DkmEvaluationResultEnumContext enumContext, int startIndex, int count, out DkmChildVisualizedExpression[] items) {
            if (count == 0) {
                items = new DkmChildVisualizedExpression[0];
                return;
            }

            var rawContextData = enumContext.GetDataItem<RawEnumContextData>();
            var rawContext = rawContextData != null ? rawContextData.RawContext : enumContext;

            var result = new List<DkmChildVisualizedExpression>(count);
            if (rawContextData != null && rawContextData.PythonView != null) {
                if (startIndex == 0) {
                    result.Add(rawContextData.PythonView);
                    --count;
                } else {
                    --startIndex;
                }
            }

            DkmEvaluationResult[] rawItems;
            visualizedExpression.GetItemsCallback(rawContext, startIndex, count, out rawItems);
            for (int i = 0; i < rawItems.Length; ++i) {
                var rawItem = rawItems[i];

                var rawSuccessItem = rawItem as DkmSuccessEvaluationResult;
                DkmExpressionValueHome home = null;
                if (rawSuccessItem != null && rawSuccessItem.Address != null) {
                    home = DkmPointerValueHome.Create(rawSuccessItem.Address.Value);
                } else {
                    home = DkmFakeValueHome.Create(0);
                }

                var item = DkmChildVisualizedExpression.Create(
                    visualizedExpression.InspectionContext,
                    visualizedExpression.VisualizerId,
                    visualizedExpression.SourceId,
                    visualizedExpression.StackFrame,
                    home,
                    rawItem,
                    visualizedExpression,
                    (uint)(startIndex + i),
                    rawItem.GetDataItem<RawEvaluationResultHolder>());
                result.Add(item);
            }

            items = result.ToArray();
        }

        public string GetUnderlyingString(DkmVisualizedExpression visualizedExpression) {
            var rawResultHolder = visualizedExpression.GetDataItem<RawEvaluationResultHolder>();
            if (rawResultHolder == null) {
                Debug.Fail("PythonViewNativeVisualizer.GetUnderlyingString passed a visualized expression that does not have an associated RawEvaluationResultHolder.");
                throw new NotSupportedException();
            }

            return visualizedExpression.GetUnderlyingStringCallback(rawResultHolder.RawResult);
        }

        public void SetValueAsString(DkmVisualizedExpression visualizedExpression, string value, int timeout, out string errorText) {
            var rawResultHolder = visualizedExpression.GetDataItem<RawEvaluationResultHolder>();
            if (rawResultHolder == null) {
                Debug.Fail("PythonViewNativeVisualizer.GetUnderlyingString passed a visualized expression that does not have an associated RawEvaluationResultHolder.");
                throw new NotSupportedException();
            }

            visualizedExpression.SetValueAsStringCallback(rawResultHolder.RawResult, value, timeout, out errorText);
        }

        public void UseDefaultEvaluationBehavior(DkmVisualizedExpression visualizedExpression, out bool useDefaultEvaluationBehavior, out DkmEvaluationResult defaultEvaluationResult) {
            // If it is a child expression, we want to fall back to default visualizer ...
            var childExpr = visualizedExpression as DkmChildVisualizedExpression;
            if (childExpr != null) {
                var childResult = childExpr.EvaluationResult;
                // ... unless the child is also of a type which uses this visualizer, in which case just recurse.
                var rawResultHolder = childResult.GetDataItem<RawEvaluationResultHolder>();
                if (rawResultHolder == null) {
                    useDefaultEvaluationBehavior = true;
                    defaultEvaluationResult = childResult;
                    return;
                } 
            }

            useDefaultEvaluationBehavior = false;
            defaultEvaluationResult = null;
        }

#endif
    }
}
