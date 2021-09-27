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

namespace Microsoft.PythonTools.Debugger.Concord
{

    /// <summary>
    ///  Represents a stored value, with a potentially non-imdepotent (if the backing store changes) and potentially expensive retrieval operation.
    /// </summary>
    internal interface IValueStore
    {
        /// <summary>
        /// Read the stored value.
        /// </summary>
        /// <remarks>
        /// This operation is not imdepotent, and can be expensive - don't repeatedly call on the same proxy unless deliberately trying to obtain a fresh value.
        /// </remarks>
        object Read();
    }

    /// <summary>
    /// Represents a stored typed value.
    /// </summary>
    internal interface IValueStore<out T> : IValueStore
    {
        new T Read();
    }

    /// <summary>
    /// A simple implementation of <see cref="IValueStore"/> which simply wraps the provided value.
    /// </summary>
    internal class ValueStore<T> : IValueStore<T>
    {
        private readonly T _value;

        public ValueStore(T value)
        {
            _value = value;
        }

        public T Read()
        {
            return _value;
        }

        object IValueStore.Read()
        {
            return Read();
        }
    }
}
