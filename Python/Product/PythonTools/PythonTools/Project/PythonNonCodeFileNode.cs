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

using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project
{
    class PythonNonCodeFileNode : CommonNonCodeFileNode
    {
        private object _designerContext;

        public PythonNonCodeFileNode(CommonProjectNode root, ProjectElement e)
            : base(root, e)
        {
        }

        class XamlCallback : IXamlDesignerCallback
        {
            private readonly PythonFileNode _node;

            public XamlCallback(PythonFileNode node)
            {
                _node = node;
            }

            public ITextBuffer Buffer
            {
                get
                {
                    return _node.GetTextBuffer();
                }
            }

            public ITextView TextView
            {
                get
                {
                    return _node.GetTextView();
                }
            }

            public bool EnsureDocumentIsOpen()
            {
                var view = TextView;
                if (view == null)
                {
                    var viewGuid = Guid.Empty;
                    if (ErrorHandler.Failed(_node.GetDocumentManager().Open(ref viewGuid, IntPtr.Zero, out _, WindowFrameShowAction.Show)))
                    {
                        return false;
                    }
                    view = TextView;
                    if (view == null)
                    {
                        return false;
                    }
                }
                return true;
            }

            public string[] FindMethods(string className, int? paramCount)
            {
                var fileInfo = _node.TryGetAnalysisEntry();
                if (fileInfo == null)
                {
                    return Array.Empty<string>();
                }
                return fileInfo.Analyzer.WaitForRequest(fileInfo.Analyzer.FindMethodsAsync(
                    fileInfo,
                    _node.GetTextBuffer(),
                    className,
                    paramCount
                ), "PythonNonCodeFileNode.FindMethods");
            }

            public InsertionPoint GetInsertionPoint(string className)
            {
                var fileInfo = _node.TryGetAnalysisEntry();
                if (fileInfo == null)
                {
                    return null;
                }
                return fileInfo.Analyzer.WaitForRequest(fileInfo.Analyzer.GetInsertionPointAsync(
                    Buffer?.CurrentSnapshot,
                    className,
                    fileInfo
                ), "PythonNonCodeFileNode.GetInsertionPoint");
            }

            public MethodInformation GetMethodInfo(string className, string methodName)
            {
                var fileInfo = _node.TryGetAnalysisEntry();
                if (fileInfo == null)
                {
                    return null;
                }
                var info = fileInfo.Analyzer.WaitForRequest(
                    fileInfo.Analyzer.GetMethodInfoAsync(fileInfo, _node.GetTextBuffer(), className, methodName),
                    "PythonNonCodeFileNode.GetMethodInfo"
                );
                if (info != null)
                {
                    return new MethodInformation(
                        info.start,
                        info.end,
                        info.found
                    );
                }
                return null;
            }
        }

        public override int QueryService(ref Guid guidService, out object result)
        {
            var model = ProjectMgr.GetService(typeof(SComponentModel)) as IComponentModel;
            var designerSupport = model?.GetService<IXamlDesignerSupport>();
            if (designerSupport != null &&
                guidService == designerSupport.DesignerContextTypeGuid &&
                Path.GetExtension(Url).Equals(".xaml", StringComparison.OrdinalIgnoreCase))
            {
                // Create a DesignerContext for the XAML designer for this file
                if (_designerContext == null)
                {
                    _designerContext = designerSupport.CreateDesignerContext();
                    var child = (
                        // look for spam.py
                        ProjectMgr.FindNodeByFullPath(Path.ChangeExtension(Url, PythonConstants.FileExtension)) ??
                        // then look for spam.pyw
                        ProjectMgr.FindNodeByFullPath(Path.ChangeExtension(Url, PythonConstants.WindowsFileExtension))
                    ) as CommonFileNode;

                    if (child != null)
                    {
                        PythonFileNode pythonNode = child as PythonFileNode;
                        if (pythonNode != null)
                        {
                            designerSupport.InitializeEventBindingProvider(
                                _designerContext,
                                new XamlCallback(pythonNode)
                            );
                        }
                    }
                }
                result = _designerContext;
                return VSConstants.S_OK;
            }

            return base.QueryService(ref guidService, out result);
        }
    }
}
