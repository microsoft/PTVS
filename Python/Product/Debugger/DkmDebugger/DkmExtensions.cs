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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Dia;
using Microsoft.PythonTools.DkmDebugger.Proxies;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Breakpoints;
using Microsoft.VisualStudio.Debugger.CallStack;
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

        public static IDiaSymbol TryGetSymbols(this DkmModuleInstance moduleInstance) {
            if (moduleInstance.Module == null) {
                return null;
            }

            var diaSession = (IDiaSession)moduleInstance.Module.GetSymbolInterface(typeof(IDiaSession).GUID);
            IDiaEnumSymbols exeSymEnum;
            diaSession.findChildren(null, SymTagEnum.SymTagExe, null, 0, out exeSymEnum);
            if (exeSymEnum.count != 1) {
                return null;
            }

            return exeSymEnum.Item(0);
        }

        public static IDiaSymbol GetSymbols(this DkmModuleInstance moduleInstance) {
            var result = TryGetSymbols(moduleInstance);
            if (result == null) {
                Debug.Fail("Failed to load symbols for module " + moduleInstance.Name);
                throw new InvalidOperationException();
            }
            return result;
        }

        public static ulong GetFunctionAddress(this DkmNativeModuleInstance moduleInstance, string name, bool debugStart = false) {
            var funcSym = moduleInstance.GetSymbols().GetSymbol(SymTagEnum.SymTagFunction, name);
            if (debugStart) {
                funcSym = funcSym.GetSymbol(SymTagEnum.SymTagFuncDebugStart, null);
            }
            return moduleInstance.BaseAddress + funcSym.relativeVirtualAddress;
        }

        public static ulong GetStaticVariableAddress(this DkmNativeModuleInstance moduleInstance, string name, string objFileName = null) {
            var symbols = moduleInstance.GetSymbols();

            if (objFileName != null) {
                var compilands = symbols.GetSymbols(SymTagEnum.SymTagCompiland, null).Where(cmp => cmp.name.EndsWith(objFileName)).ToArray();
                if (compilands.Length == 0) {
                    Debug.Fail("Compiland '" + objFileName + "' was not found.");
                    throw new ArgumentException();
                } else if (compilands.Length > 1) {
                    Debug.Fail("Found more than one compiland named '" + objFileName + "'.");
                    throw new ArgumentException();
                } else {
                    symbols = compilands[0];
                }
            }

            var varSym = symbols.GetSymbol(SymTagEnum.SymTagData, name);
            return moduleInstance.BaseAddress + varSym.relativeVirtualAddress;
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
