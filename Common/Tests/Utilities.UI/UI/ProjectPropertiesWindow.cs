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

namespace TestUtilities.UI
{
    class ProjectPropertiesWindow : AutomationWrapper
    {
        public ProjectPropertiesWindow(IntPtr element)
            : base(AutomationElement.FromHandle(element))
        {
        }

        public AutomationElement this[Guid tabGuid]
        {
            get
            {

                var tabItem = FindByAutomationId("PropPage_" + tabGuid.ToString("n").ToLower());
                Assert.IsNotNull(tabItem, "Failed to find page");

                AutomationWrapper.DumpElement(tabItem);
                foreach (var p in tabItem.GetSupportedPatterns())
                {
                    Console.WriteLine("Supports {0}", p.ProgrammaticName);
                }

                try
                {
                    tabItem.GetInvokePattern().Invoke();
                }
                catch (InvalidOperationException)
                {
                    AutomationWrapper.DoDefaultAction(tabItem);
                }

                return FindByAutomationId("PageHostingPanel");
            }
        }
    }
}
