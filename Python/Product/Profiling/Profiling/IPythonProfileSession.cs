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

namespace Microsoft.PythonTools.Profiling
{
    [Guid("20F87722-745A-48C7-B9D5-DD9B85F96B7F")]
    public interface IPythonProfileSession
    {
        string Name
        {
            get;
        }

        string Filename
        {
            get;
        }

        IPythonPerformanceReport GetReport(object item);

        void Save(string filename = null);

        void Launch(bool openReport = false);

        bool IsSaved
        {
            get;
        }
    }
}
