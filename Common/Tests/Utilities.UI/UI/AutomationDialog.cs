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
using System.Linq;
using System.Windows.Automation;

namespace TestUtilities.UI
{
    public class AutomationDialog : AutomationWrapper, IDisposable
    {
        private bool _isDisposed;

        public VisualStudioApp App { get; private set; }
        public TimeSpan DefaultTimeout { get; set; }

        public AutomationDialog(VisualStudioApp app, AutomationElement element)
            : base(element)
        {
            App = app;
            DefaultTimeout = TimeSpan.FromSeconds(10.0);
        }

        public static AutomationDialog FromDte(VisualStudioApp app, string commandName, string commandArgs = "")
        {
            return new AutomationDialog(
                app,
                AutomationElement.FromHandle(app.OpenDialogWithDteExecuteCommand(commandName, commandArgs))
            );
        }

        public static AutomationDialog WaitForDialog(VisualStudioApp app)
        {
            return new AutomationDialog(app, AutomationElement.FromHandle(app.WaitForDialog()));
        }

        #region IDisposable Members

        ~AutomationDialog()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    try
                    {
                        Element.GetWindowPattern().Close();
                    }
                    catch (InvalidOperationException)
                    {
                    }
                    catch (ElementNotAvailableException)
                    {
                    }
                }
                _isDisposed = true;
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        public bool ClickButtonAndClose(string buttonName, bool nameIsAutomationId = false)
        {
            WaitForInputIdle();
            if (nameIsAutomationId)
            {
                return WaitForClosed(DefaultTimeout, () => ClickButtonByAutomationId(buttonName));
            }
            else if (buttonName == "Cancel")
            {
                return WaitForClosed(DefaultTimeout, () =>
                {
                    var btn = Element.FindFirst(TreeScope.Descendants, new AndCondition(
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                        new OrCondition(
                            new PropertyCondition(AutomationElement.NameProperty, "Cancel"),
                            new PropertyCondition(AutomationElement.NameProperty, "Close")
                        )
                    ));
                    CheckNullElement(btn);
                    Invoke(btn);
                });
            }
            else
            {
                if (buttonName == "Ok")
                {
                    // Need a case-sensitive match, even with the IgnoreCase flags
                    buttonName = "OK";
                }
                return WaitForClosed(DefaultTimeout, () => ClickButtonByName(buttonName));
            }
        }

        public virtual void OK()
        {
            ClickButtonAndClose("OK");
        }

        public virtual void Cancel()
        {
            ClickButtonAndClose("Cancel");
        }

        public virtual string Text
        {
            get
            {
                string label = string.Join(Environment.NewLine,
                    FindAllByControlType(ControlType.Text).Cast<AutomationElement>().Select(a => a.Current.Name ?? "")
                );
                return label ?? "";
            }
        }
    }
}
