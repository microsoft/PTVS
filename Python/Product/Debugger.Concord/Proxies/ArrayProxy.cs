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

namespace Microsoft.PythonTools.Debugger.Concord.Proxies
{
    /// <summary>
    /// A data proxy for an array of elements of indeterminate size.
    /// </summary>
    /// <remarks>
    /// This proxy does not know the size of the actual array, and so it doesn't support most normal proxy operations (size, read, write).
    /// It has to implement <see cref="IDataProxy"/> so that you can have pointers to arrays and arrays of arrays, but it should never be
    /// passed to any code that expects a generic <see cref="IValueStore"/>.
    /// </remarks>
    internal struct ArrayProxy<TProxy> : IDataProxy, IEnumerable<TProxy>
        where TProxy : IDataProxy
    {

        public DkmProcess Process { get; private set; }
        public ulong Address { get; private set; }

        public ArrayProxy(DkmProcess process, ulong address)
            : this()
        {
            Debug.Assert(process != null && address != 0);
            Process = process;
            Address = address;
        }

        long IDataProxy.ObjectSize
        {
            get { throw new NotSupportedException(); }
        }

        object IValueStore.Read()
        {
            throw new NotSupportedException();
        }

        public TProxy this[long index]
        {
            get
            {
                var result = DataProxy.Create<TProxy>(Process, Address);
                if (index != 0)
                {
                    result = result.GetAdjacentProxy(index);
                }
                return result;
            }
        }

        /// <summary>
        /// Enumerates elements in the array and returns a sequence of proxies for them. The returned sequence is lazy and unbounded,
        /// so the caller should either count the elements, or look for some sentinel value to know when to stop reading.
        /// </summary>
        public IEnumerator<TProxy> GetEnumerator()
        {
            var element = this[0];
            while (true)
            {
                yield return element;
                element = element.GetAdjacentProxy(1);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
