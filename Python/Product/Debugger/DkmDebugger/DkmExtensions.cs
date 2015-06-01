/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.Dia;
using Microsoft.PythonTools.Debugger;
using Microsoft.PythonTools.DkmDebugger.Proxies;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Breakpoints;
using Microsoft.VisualStudio.Debugger.CustomRuntimes;
using Microsoft.VisualStudio.Debugger.DefaultPort;
using Microsoft.VisualStudio.Debugger.Native;

namespace Microsoft.PythonTools.DkmDebugger {
    internal static unsafe class DkmExtensions {
        public static T GetOrCreateDataItem<T>(this DkmDataContainer container, Func<T> factory)
            where T : DkmDataItem {
            var result = container.GetDataItem<T>();
            if (result == null) {
                result = factory();
                container.SetDataItem(DkmDataCreationDisposition.CreateNew, result);
            }
            return result;
        }

        public static bool Is64Bit(this DkmProcess process) {
            return (process.SystemInformation.Flags & DkmSystemInformationFlags.Is64Bit) != 0;
        }

        public static byte GetPointerSize(this DkmProcess process) {
            return process.Is64Bit() ? (byte)8 : (byte)4;
        }

        public static bool ContainsAddress(this DkmNativeModuleInstance instance, ulong address) {
            if (instance == null) {
                return false;
            }
            var baseAddr = instance.BaseAddress;
            return address >= baseAddr && address < baseAddr + instance.Size;
        }

        public static DkmRuntimeInstructionBreakpoint CreateBreakpoint(this DkmProcess process, Guid sourceId, ulong address) {
            var iaddr = process.CreateNativeInstructionAddress(address);
            return DkmRuntimeInstructionBreakpoint.Create(sourceId, null, iaddr, false, null);
        }

        public static ulong GetPointer(this DkmNativeInstructionAddress addr) {
            return addr.RVA + addr.ModuleInstance.BaseAddress;
        }

        public static DkmCustomRuntimeInstance GetPythonRuntimeInstance(this DkmProcess process) {
            return (DkmCustomRuntimeInstance)process.GetRuntimeInstances().FirstOrDefault(rti => rti.Id.RuntimeType == Guids.PythonRuntimeTypeGuid);
        }

        private class ModuleInstances : DkmDataItem {
            public DkmNativeModuleInstance PythonDll { get; set; }
            public DkmNativeModuleInstance DebuggerHelperDll { get; set; }
        }

        public static ComPtr<IDiaSymbol> TryGetSymbols(this DkmModuleInstance moduleInstance) {
            if (moduleInstance.Module == null) {
                return new ComPtr<IDiaSymbol>();
            }

            IDiaSession diaSession;
            try {
                diaSession = (IDiaSession)moduleInstance.Module.GetSymbolInterface(typeof(IDiaSession).GUID);
            } catch (InvalidCastException) {
                // GetSymbolInterface will throw this if it did locate a symbol provider object, but QueryInterface for the GUID failed with E_NOINTERFACE.
                // Since this means that we cannot use the symbol provider for anything useful, treat it as absence of symbol information.
                return new ComPtr<IDiaSymbol>();
            }

            using (ComPtr.Create(diaSession)) {
                IDiaEnumSymbols exeSymEnum;
                diaSession.findChildren(null, SymTagEnum.SymTagExe, null, 0, out exeSymEnum);
                using (ComPtr.Create(exeSymEnum)) {
                    if (exeSymEnum.count != 1) {
                        return new ComPtr<IDiaSymbol>();
                    }

                    return ComPtr.Create(exeSymEnum.Item(0));
                }
            }
        }

        public static bool HasSymbols(this DkmModuleInstance moduleInstance) {
            using (var sym = moduleInstance.TryGetSymbols()) {
                return sym.Object != null;
            }
        }

        public static ComPtr<IDiaSymbol> GetSymbols(this DkmModuleInstance moduleInstance) {
            var result = TryGetSymbols(moduleInstance);
            if (result.Object == null) {
                Debug.Fail("Failed to load symbols for module " + moduleInstance.Name);
                throw new InvalidOperationException();
            }
            return result;
        }

        public static ulong GetFunctionAddress(this DkmNativeModuleInstance moduleInstance, string name, bool debugStart = false) {
            uint rva;
            using (var moduleSym = moduleInstance.GetSymbols()) {
                using (var funcSym = moduleSym.Object.GetSymbol(SymTagEnum.SymTagFunction, name)) {
                    if (debugStart) {
                        using (var startSym = funcSym.Object.GetSymbol(SymTagEnum.SymTagFuncDebugStart, null)) {
                            rva = startSym.Object.relativeVirtualAddress;
                        }
                    } else {
                        rva = funcSym.Object.relativeVirtualAddress;
                    }
                }
            }
            return moduleInstance.BaseAddress + rva;
        }

        public static ulong GetStaticVariableAddress(this DkmNativeModuleInstance moduleInstance, string name, string objFileName = null) {
            uint rva;
            using (var moduleSym = moduleInstance.GetSymbols()) {
                if (objFileName != null) {
                    using (var compiland = moduleSym.Object.GetSymbol(SymTagEnum.SymTagCompiland, null, cmp => cmp.name.EndsWith(objFileName)))
                    using (var varSym = compiland.Object.GetSymbol(SymTagEnum.SymTagData, name)) {
                        rva = varSym.Object.relativeVirtualAddress;
                    }
                } else {
                    using (var varSym = moduleSym.Object.GetSymbol(SymTagEnum.SymTagData, name)) {
                        rva = varSym.Object.relativeVirtualAddress;
                    }
                }
            }
            return moduleInstance.BaseAddress + rva;
        }

        public static TProxy GetStaticVariable<TProxy>(this DkmNativeModuleInstance moduleInstance, string name, string objFileName = null)
            where TProxy : IDataProxy {
            ulong address = GetStaticVariableAddress(moduleInstance, name, objFileName);
            return DataProxy.Create<TProxy>(moduleInstance.Process, address);
        }

        public static ulong GetExportedStaticVariableAddress(this DkmNativeModuleInstance moduleInstance, string name) {
            var addr = moduleInstance.FindExportName(name, false);
            if (addr == null) { 
                Debug.Fail("Couldn't find dllexport variable " + name + " in module " + moduleInstance.Name);
                throw new ArgumentException();
            }
            return moduleInstance.BaseAddress + addr.RVA;
        }

        public static TProxy GetExportedStaticVariable<TProxy>(this DkmNativeModuleInstance moduleInstance, string name)
            where TProxy : IDataProxy {
            ulong address = GetExportedStaticVariableAddress(moduleInstance, name);
            return DataProxy.Create<TProxy>(moduleInstance.Process, address);
        }

        public static DkmNativeInstructionAddress GetExportedFunctionAddress(this DkmNativeModuleInstance moduleInstance, string name) {
            var addr = moduleInstance.FindExportName(name, true);
            if (addr == null) {
                Debug.Fail("Couldn't find dllexport function " + name + " in module " + moduleInstance.Name);
                throw new ArgumentException();
            }
            return addr;
        }

        public static ulong OffsetBy(this ulong address, long offset) {
            return unchecked((ulong)((long)address + offset));
        }
    }
}
