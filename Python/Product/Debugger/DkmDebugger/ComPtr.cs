using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.DkmDebugger {
    /// <summary>
    /// An owning reference to a COM object. When disposed, calls <see cref="Marshal.ReleaseComObject"/> on the pointee.
    /// </summary>
    internal struct ComPtr<T> : IDisposable
        where T : class {

        private T _obj;

        public ComPtr(T obj) {
            _obj = obj;
        }

        public T Object {
            get {
                return _obj;
            }
        }

        public void Dispose() {
            if (_obj != null && Marshal.IsComObject(_obj)) {
                Marshal.ReleaseComObject(_obj);
            }
            _obj = null;
        }

        public ComPtr<T> Detach() {
            var result = this;
            _obj = null;
            return result;
        }

        public static implicit operator T (ComPtr<T> ptr) {
            return ptr._obj;
        }
    }

    internal static class ComPtr {
        public static ComPtr<T> Create<T>(T obj) where T : class {
            return new ComPtr<T>(obj);
        }
    }
}
