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
using System.Collections.Generic;

namespace TestUtilities.Mocks
{
#pragma warning disable CS0246 // The type or namespace name 'IVsShell' could not be found (are you missing a using directive or an assembly reference?)
#pragma warning disable CS0246 // The type or namespace name 'IVsShell' could not be found (are you missing a using directive or an assembly reference?)
    public class MockVsShell : IVsShell
#pragma warning restore CS0246 // The type or namespace name 'IVsShell' could not be found (are you missing a using directive or an assembly reference?)
#pragma warning restore CS0246 // The type or namespace name 'IVsShell' could not be found (are you missing a using directive or an assembly reference?)
    {
        public readonly Dictionary<int, object> Properties = new Dictionary<int, object>();
        public readonly object ReadOnlyPropertyValue = new object();
#pragma warning disable CS0246 // The type or namespace name 'IVsShellPropertyEvents' could not be found (are you missing a using directive or an assembly reference?)
#pragma warning disable CS0246 // The type or namespace name 'IVsShellPropertyEvents' could not be found (are you missing a using directive or an assembly reference?)
        private readonly List<IVsShellPropertyEvents> _listeners = new List<IVsShellPropertyEvents>();
#pragma warning restore CS0246 // The type or namespace name 'IVsShellPropertyEvents' could not be found (are you missing a using directive or an assembly reference?)
#pragma warning restore CS0246 // The type or namespace name 'IVsShellPropertyEvents' could not be found (are you missing a using directive or an assembly reference?)

        public int GetProperty(int propid, out object pvar)
        {
            if (Properties.TryGetValue(propid, out pvar))
            {
                Console.WriteLine("MockVsShell.GetProperty(propid={0}) -> {1}", propid, pvar);
                return VSConstants.S_OK;
            }
            Console.WriteLine("MockVsShell.GetProperty(propid={0}) -> <unknown value>", propid);
            return VSConstants.E_INVALIDARG;
        }

        public int SetProperty(int propid, object var)
        {
            object value;
            if (Properties.TryGetValue(propid, out value))
            {
                if (value == ReadOnlyPropertyValue)
                {
                    Console.WriteLine("MockVsShell.SetProperty(propid={0}, var={1}) -> E_INVALIDARG", propid, var);
                    return VSConstants.E_INVALIDARG;
                }
                Console.WriteLine("MockVsShell.SetProperty(propid={0}, var={1}) replacing {2}", propid, var, value);
            }
            else
            {
                Console.WriteLine("MockVsShell.SetProperty(propid={0}, var={1})", propid, var);
            }
            Properties[propid] = var;
            foreach (var l in _listeners)
            {
                l?.OnShellPropertyChange(propid, var);
            }
            return VSConstants.S_OK;
        }


#pragma warning disable CS0246 // The type or namespace name 'IVsBroadcastMessageEvents' could not be found (are you missing a using directive or an assembly reference?)
#pragma warning disable CS0246 // The type or namespace name 'IVsBroadcastMessageEvents' could not be found (are you missing a using directive or an assembly reference?)
        public int AdviseBroadcastMessages(IVsBroadcastMessageEvents pSink, out uint pdwCookie)
#pragma warning restore CS0246 // The type or namespace name 'IVsBroadcastMessageEvents' could not be found (are you missing a using directive or an assembly reference?)
#pragma warning restore CS0246 // The type or namespace name 'IVsBroadcastMessageEvents' could not be found (are you missing a using directive or an assembly reference?)
        {
            throw new NotImplementedException();
        }

#pragma warning disable CS0246 // The type or namespace name 'IVsShellPropertyEvents' could not be found (are you missing a using directive or an assembly reference?)
#pragma warning disable CS0246 // The type or namespace name 'IVsShellPropertyEvents' could not be found (are you missing a using directive or an assembly reference?)
        public int AdviseShellPropertyChanges(IVsShellPropertyEvents pSink, out uint pdwCookie)
#pragma warning restore CS0246 // The type or namespace name 'IVsShellPropertyEvents' could not be found (are you missing a using directive or an assembly reference?)
#pragma warning restore CS0246 // The type or namespace name 'IVsShellPropertyEvents' could not be found (are you missing a using directive or an assembly reference?)
        {
            _listeners.Add(pSink);
            pdwCookie = (uint)_listeners.Count - 1;
            return VSConstants.S_OK;
        }

#pragma warning disable CS0246 // The type or namespace name 'IEnumPackages' could not be found (are you missing a using directive or an assembly reference?)
#pragma warning disable CS0246 // The type or namespace name 'IEnumPackages' could not be found (are you missing a using directive or an assembly reference?)
        public int GetPackageEnum(out IEnumPackages ppenum)
#pragma warning restore CS0246 // The type or namespace name 'IEnumPackages' could not be found (are you missing a using directive or an assembly reference?)
#pragma warning restore CS0246 // The type or namespace name 'IEnumPackages' could not be found (are you missing a using directive or an assembly reference?)
        {
            throw new NotImplementedException();
        }

        public int IsPackageInstalled(ref Guid guidPackage, out int pfInstalled)
        {
            throw new NotImplementedException();
        }

#pragma warning disable CS0246 // The type or namespace name 'IVsPackage' could not be found (are you missing a using directive or an assembly reference?)
#pragma warning disable CS0246 // The type or namespace name 'IVsPackage' could not be found (are you missing a using directive or an assembly reference?)
        public int IsPackageLoaded(ref Guid guidPackage, out IVsPackage ppPackage)
#pragma warning restore CS0246 // The type or namespace name 'IVsPackage' could not be found (are you missing a using directive or an assembly reference?)
#pragma warning restore CS0246 // The type or namespace name 'IVsPackage' could not be found (are you missing a using directive or an assembly reference?)
        {
            throw new NotImplementedException();
        }

#pragma warning disable CS0246 // The type or namespace name 'IVsPackage' could not be found (are you missing a using directive or an assembly reference?)
#pragma warning disable CS0246 // The type or namespace name 'IVsPackage' could not be found (are you missing a using directive or an assembly reference?)
        public int LoadPackage(ref Guid guidPackage, out IVsPackage ppPackage)
#pragma warning restore CS0246 // The type or namespace name 'IVsPackage' could not be found (are you missing a using directive or an assembly reference?)
#pragma warning restore CS0246 // The type or namespace name 'IVsPackage' could not be found (are you missing a using directive or an assembly reference?)
        {
            throw new NotImplementedException();
        }

        public int LoadPackageString(ref Guid guidPackage, uint resid, out string pbstrOut)
        {
            throw new NotImplementedException();
        }

        public int LoadUILibrary(ref Guid guidPackage, uint dwExFlags, out uint phinstOut)
        {
            throw new NotImplementedException();
        }

        public int UnadviseBroadcastMessages(uint dwCookie)
        {
            throw new NotImplementedException();
        }

        public int UnadviseShellPropertyChanges(uint dwCookie)
        {
            _listeners[(int)dwCookie] = null;
            return VSConstants.S_OK;
        }
    }
}
