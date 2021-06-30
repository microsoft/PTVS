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


using EnvDTE;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.VisualStudioTools.MockVsTests
{
    internal class MockDTEProperties : EnvDTE.Properties
    {
        private readonly Dictionary<string, Property> _properties = new Dictionary<string, Property>();

        public MockDTEProperties()
        {
        }

        public object Application
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public int Count
        {
            get
            {
                return _properties.Count;
            }
        }

        public DTE DTE
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public object Parent
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public IEnumerator GetEnumerator()
        {
            throw new NotImplementedException();
        }

        public Property Item(object index)
        {
            return _properties[(string)index];
        }

        public void Add(string name, object value)
        {
            _properties.Add(name, new MockDTEProperty(value));
        }
    }
}