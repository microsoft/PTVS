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

using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace TestUtilities.Mocks
{
    [Export(typeof(IContentTypeRegistryService))]
    public class MockContentTypeRegistryService : IContentTypeRegistryService
    {
        private readonly Dictionary<string, MockContentType> _contentTypes = new Dictionary<string, MockContentType>(StringComparer.InvariantCultureIgnoreCase);
        private const string _unknownName = "UNKNOWN";
        private readonly MockContentType _unknownType;
        private static string[] Empty = new string[0];

        public MockContentTypeRegistryService()
        {
            _contentTypes[_unknownName] = _unknownType = new MockContentType(_unknownName, new IContentType[0]);
        }

        public MockContentTypeRegistryService(params string[] existingNames) : this()
        {
            foreach (var type in existingNames)
            {
                AddContentType(type, new string[0]);
            }
        }

        #region IContentTypeRegistryService Members

        public IContentType AddContentType(string typeName, IEnumerable<string> baseTypeNames)
        {
            if (typeName == null)
            {
                throw new ArgumentNullException();
            }

            string uppercaseTypeName = typeName.ToUpperInvariant();
            if (uppercaseTypeName == _unknownName)
            {
                throw new InvalidOperationException();
            }


            MockContentType type;
            if (!_contentTypes.TryGetValue(typeName, out type))
            {
                _contentTypes[typeName] = new MockContentType(typeName, new IContentType[0]);
            }
            List<IContentType> baseTypes = new List<IContentType>();
            foreach (var baseTypeName in baseTypeNames)
            {
                baseTypes.Add(AddContentType(baseTypeName, Empty));
            }
            return type;
        }

        public IEnumerable<IContentType> ContentTypes
        {
            get { return _contentTypes.Values; }
        }

        public IContentType GetContentType(string typeName)
        {
            MockContentType res;
            if (_contentTypes.TryGetValue(typeName, out res))
            {
                return res;
            }
            throw new InvalidOperationException("Unknown content type: " + typeName);
        }

        public void RemoveContentType(string typeName)
        {
            string uppercaseTypeName = typeName.ToUpperInvariant();
            if (uppercaseTypeName == _unknownName)
            {
                throw new InvalidOperationException();
            }
            _contentTypes.Remove(typeName);
        }

        public IContentType UnknownContentType
        {
            get { return _unknownType; }
        }

        #endregion
    }
}
