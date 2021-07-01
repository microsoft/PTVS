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

namespace Microsoft.PythonTools.Refactoring
{
    /// <summary>
    /// Handles input when running the rename refactoring within Visual Studio.
    /// </summary>
    class RenameVariableUserInput : IRenameVariableInput
    {
        private readonly IServiceProvider _serviceProvider;

        public const string RefactorGuidStr = "{5A822660-832B-4AF0-9A86-1048D33A05E7}";
        private static readonly Guid RefactorGuid = new Guid(RefactorGuidStr);
        private const string RefactorKey = "Refactor";
        private const string RenameKey = "Rename";
        private const string PreviewChangesKey = "PreviewChanges";

        public RenameVariableUserInput(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public RenameVariableRequest GetRenameInfo(string originalName, PythonLanguageVersion languageVersion)
        {
            var requestView = new RenameVariableRequestView(originalName, languageVersion);
            LoadPreferences(requestView);
            var dialog = new RenameVariableDialog(requestView);
            bool res = dialog.ShowModal() ?? false;
            if (res)
            {
                SavePreferences(requestView);
                return requestView.GetRequest();
            }

            return null;
        }

        private void SavePreferences(RenameVariableRequestView requestView)
        {
            SaveBool(PreviewChangesKey, requestView.PreviewChanges);
        }

        private void LoadPreferences(RenameVariableRequestView requestView)
        {
            requestView.PreviewChanges = LoadBool(PreviewChangesKey) ?? true;
        }

        internal void SaveBool(string name, bool value)
        {
            SaveString(name, value.ToString());
        }

        internal void SaveString(string name, string value)
        {
            using (var pythonKey = VSRegistry.RegistryRoot(__VsLocalRegistryType.RegType_UserSettings, true).CreateSubKey(PythonCoreConstants.BaseRegistryKey))
            {
                using (var refactorKey = pythonKey.CreateSubKey(RefactorKey))
                {
                    using (var renameKey = refactorKey.CreateSubKey(RenameKey))
                    {
                        renameKey.SetValue(name, value, Win32.RegistryValueKind.String);
                    }
                }
            }
        }

        internal bool? LoadBool(string name)
        {
            string res = LoadString(name);
            if (res == null)
            {
                return null;
            }

            bool val;
            if (bool.TryParse(res, out val))
            {
                return val;
            }
            return null;
        }

        internal string LoadString(string name)
        {
            using (var pythonKey = VSRegistry.RegistryRoot(__VsLocalRegistryType.RegType_UserSettings, true).CreateSubKey(PythonCoreConstants.BaseRegistryKey))
            {
                using (var refactorKey = pythonKey.CreateSubKey(RefactorKey))
                {
                    using (var renameKey = refactorKey.CreateSubKey(RenameKey))
                    {
                        return renameKey.GetValue(name) as string;
                    }
                }
            }
        }

        public void CannotRename(string message)
        {
            MessageBox.Show(message, Strings.RenameVariable_CannotRenameTitle, MessageBoxButton.OK);
        }

        public void OutputLog(string message)
        {
            IVsOutputWindowPane pane = GetPane();
            if (pane != null)
            {
                pane.Activate();

                pane.OutputString(message);
                pane.OutputString(Environment.NewLine);
            }
        }

        public void ClearRefactorPane()
        {
            IVsOutputWindowPane pane = GetPane();
            if (pane != null)
            {
                pane.Clear();
            }
        }

        private IVsOutputWindowPane GetPane()
        {
            IVsOutputWindowPane pane;
            var outWin = (IVsOutputWindow)_serviceProvider.GetService(typeof(IVsOutputWindow));

            char[] buffer = new char[1024];
            Guid tmp = RefactorGuid;

            if (!ErrorHandler.Succeeded(outWin.GetPane(ref tmp, out pane)))
            {
                ErrorHandler.ThrowOnFailure(outWin.CreatePane(ref tmp, Strings.RefactorPaneName, 1, 1));

                if (!ErrorHandler.Succeeded(outWin.GetPane(ref tmp, out pane)))
                {
                    return null;
                }
            }
            return pane;
        }

        public ITextBuffer GetBufferForDocument(string filename)
        {
            return PythonToolsPackage.GetBufferForDocument(_serviceProvider, filename);
        }


        public IVsLinkedUndoTransactionManager BeginGlobalUndo()
        {
            var linkedUndo = (IVsLinkedUndoTransactionManager)_serviceProvider.GetService(typeof(SVsLinkedUndoTransactionManager));
            ErrorHandler.ThrowOnFailure(linkedUndo.OpenLinkedUndo(
                (uint)LinkedTransactionFlags2.mdtGlobal,
                Strings.RefactorRenameVariableUndoDescription
            ));
            return linkedUndo;
        }


        public void EndGlobalUndo(IVsLinkedUndoTransactionManager linkedUndo)
        {
            ErrorHandler.ThrowOnFailure(linkedUndo.CloseLinkedUndo());
        }
    }
}
