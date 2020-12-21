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

using System;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Input;

namespace TestUtilities.UI
{
    public class SaveDialog : AutomationDialog
    {
        public SaveDialog(VisualStudioApp app, AutomationElement element)
            : base(app, element)
        {
        }

        public static SaveDialog FromDte(VisualStudioApp app)
        {
            return new SaveDialog(
                app,
                AutomationElement.FromHandle(app.OpenDialogWithDteExecuteCommand("File.SaveSelectedItemsAs"))
            );
        }

        public void Save()
        {
            WaitForInputIdle();
            // The Save button on this dialog is broken and so UIA cannot invoke
            // it (though somehow Inspect is able to...). We use the keyboard
            // instead.
            WaitForClosed(DefaultTimeout, () => Keyboard.PressAndRelease(Key.S, Key.LeftAlt));
        }

        public override void OK()
        {
            Save();
        }

        public string FileName
        {
            get
            {
                return GetFilenameEditBox().GetValuePattern().Current.Value;
            }
            set
            {
                GetFilenameEditBox().GetValuePattern().SetValue(value);
            }
        }

        private AutomationElement GetFilenameEditBox()
        {
            return FindByAutomationId("FileNameControlHost");
        }
    }
}
