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
using TestUtilities;

namespace Microsoft.VisualStudioTools.MockVsTests
{
    class MockDialog
    {
        public readonly string Title;
        public readonly MockVs Vs;
        public int DialogResult = 0;
        private AutoResetEvent _dismiss = new AutoResetEvent(false);

        public MockDialog(MockVs vs, string title)
        {
            Title = title;
            Vs = vs;
        }

        public virtual void Type(string text)
        {
            switch (text)
            {
                case "\r":
                    Close((int)MessageBoxButton.Ok);
                    break;
                default:
                    throw new NotImplementedException("Unhandled dialog text: " + text);
            }
        }

        public virtual void Run()
        {
            Vs.RunMessageLoop(_dismiss);
        }

        public virtual void Close(int result)
        {
            DialogResult = result;
            _dismiss.Set();
        }
    }
}
