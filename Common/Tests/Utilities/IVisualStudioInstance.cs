// Visual Studio Shared Project
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

namespace TestUtilities
{
    public interface IVisualStudioInstance : IDisposable
    {
        void Type(Key key);

        void Type(string p);

        void ControlC();
        void ControlV();

        void ControlX();

        void CheckMessageBox(params string[] text);
        void CheckMessageBox(MessageBoxButton button, params string[] text);
        void MaybeCheckMessageBox(MessageBoxButton button, params string[] text);

        ITreeNode WaitForItem(params string[] items);

        ITreeNode FindItem(params string[] items);

        IEditor OpenItem(string project, params string[] path);

        ITreeNode WaitForItemRemoved(params string[] path);

        void WaitForOutputWindowText(string name, string containsText, int timeout = 5000);


        void Sleep(int ms);

        void ExecuteCommand(string command);

        string SolutionFilename { get; }
        string SolutionDirectory { get; }

        IntPtr WaitForDialog();

        void WaitForDialogDismissed();

        void AssertFileExists(params string[] path);

        void AssertFileDoesntExist(params string[] path);

        void AssertFolderExists(params string[] path);

        void AssertFolderDoesntExist(params string[] path);

        void AssertFileExistsWithContent(string content, params string[] path);

        void CloseActiveWindow(vsSaveChanges save);

        IntPtr OpenDialogWithDteExecuteCommand(string commandName, string commandArgs = "");

        void SelectSolutionNode();

        Project GetProject(string projectName);

        void SelectProject(Project project);

        IEditor GetDocument(string filename);

        IAddExistingItem AddExistingItem();

        IAddNewItem AddNewItem();

        IOverwriteFile WaitForOverwriteFileDialog();

        void WaitForMode(dbgDebugMode dbgDebugMode);

#pragma warning disable CS0246 // The type or namespace name 'IVsTaskItem' could not be found (are you missing a using directive or an assembly reference?)
#pragma warning disable CS0246 // The type or namespace name 'IVsTaskItem' could not be found (are you missing a using directive or an assembly reference?)
        List<IVsTaskItem> WaitForErrorListItems(int expectedCount);
#pragma warning restore CS0246 // The type or namespace name 'IVsTaskItem' could not be found (are you missing a using directive or an assembly reference?)
#pragma warning restore CS0246 // The type or namespace name 'IVsTaskItem' could not be found (are you missing a using directive or an assembly reference?)

        DTE Dte { get; }

        void OnDispose(Action action);
        void PressAndRelease(Key key, params Key[] modifier);
    }
}
