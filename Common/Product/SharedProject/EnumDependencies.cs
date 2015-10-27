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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Collections.Generic;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudioTools.Project {

    public class EnumDependencies : IVsEnumDependencies {
        private List<IVsDependency> dependencyList = new List<IVsDependency>();

        private uint nextIndex;

        public EnumDependencies(IList<IVsDependency> dependencyList) {
            Utilities.ArgumentNotNull("dependencyList", dependencyList);

            foreach (IVsDependency dependency in dependencyList) {
                this.dependencyList.Add(dependency);
            }
        }

        public EnumDependencies(IList<IVsBuildDependency> dependencyList) {
            Utilities.ArgumentNotNull("dependencyList", dependencyList);

            foreach (IVsBuildDependency dependency in dependencyList) {
                this.dependencyList.Add(dependency);
            }
        }

        public int Clone(out IVsEnumDependencies enumDependencies) {
            enumDependencies = new EnumDependencies(this.dependencyList);
            ErrorHandler.ThrowOnFailure(enumDependencies.Skip(this.nextIndex));
            return VSConstants.S_OK;
        }

        public int Next(uint elements, IVsDependency[] dependencies, out uint elementsFetched) {
            elementsFetched = 0;
            Utilities.ArgumentNotNull("dependencies", dependencies);

            uint fetched = 0;
            int count = this.dependencyList.Count;

            while (this.nextIndex < count && elements > 0 && fetched < count) {
                dependencies[fetched] = this.dependencyList[(int)this.nextIndex];
                this.nextIndex++;
                fetched++;
                elements--;

            }

            elementsFetched = fetched;

            // Did we get 'em all?
            return (elements == 0 ? VSConstants.S_OK : VSConstants.S_FALSE);
        }

        public int Reset() {
            this.nextIndex = 0;
            return VSConstants.S_OK;
        }

        public int Skip(uint elements) {
            this.nextIndex += elements;
            uint count = (uint)this.dependencyList.Count;

            if (this.nextIndex > count) {
                this.nextIndex = count;
                return VSConstants.S_FALSE;
            }

            return VSConstants.S_OK;
        }
    }
}
