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
namespace Microsoft.VisualStudioTools.MockVsTests
{
    /// <summary>
    /// Performs initialization of a mock VS package.
    /// 
    /// Initializing a real MPF Package class inside of MockVs is not actually possible  
    /// 
    /// Despite using siting, MPF actually goes off to global service providers for various
    /// activities.  For example it uses the ActivityLog class which does not get properly
    /// sited.  
    /// 
    /// To use MockVs packages should abstract most of the code from their package into an
    /// independent service and have their package publish (and promote) their service.  Mock
    /// packages can then do the same thing.
    /// </summary>
    public interface IMockPackage : IDisposable
    {
        void Initialize();
    }
}
