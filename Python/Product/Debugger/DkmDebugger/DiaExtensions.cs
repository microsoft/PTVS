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
using Microsoft.Dia;
using Microsoft.VisualStudio.Debugger.Native;

namespace Microsoft.PythonTools.DkmDebugger {
    internal enum DiaLocationType : uint {
        LocIsNull,
        LocIsStatic,
        LocIsTLS,
        LocIsRegRel,
        LocIsThisRel,
        LocIsEnregistered,
        LocIsBitField,
        LocIsSlot,
        LocIsIlRel,
        LocInMetaData,
        LocIsConstant,
        LocTypeMax
    }

    internal static class DiaExtensions {
        public static ComPtr<IDiaSymbol>[] GetSymbols(this IDiaSymbol symbol, SymTagEnum symTag, string name) {
            IDiaEnumSymbols enumSymbols;
            symbol.findChildren(symTag, name, 1, out enumSymbols);
            using (ComPtr.Create(enumSymbols)) {
                int n = enumSymbols.count;
                var result = new ComPtr<IDiaSymbol>[n];
                try {
                    for (int i = 0; i < n; ++i) {
                        result[i] = ComPtr.Create(enumSymbols.Item((uint)i));
                    }
                } catch {
                    foreach (var item in result) {
                        item.Dispose();
                    }
                    throw;
                }
                return result;
            }
        }

        public static ComPtr<IDiaSymbol> GetSymbol(this IDiaSymbol symbol, SymTagEnum symTag, string name, Predicate<IDiaSymbol> filter = null) {
            var result = new ComPtr<IDiaSymbol>();

            IDiaEnumSymbols enumSymbols;
            symbol.findChildren(symTag, name, 1, out enumSymbols);
            using (ComPtr.Create(enumSymbols)) {
                int n = enumSymbols.count;
                if (n == 0) {
                    Debug.Fail("Symbol '" + name + "' was not found.");
                    throw new ArgumentException();
                }

                try {
                    for (int i = 0; i < n; ++i) {
                        using (var item = ComPtr.Create(enumSymbols.Item((uint)i))) {
                            if (filter == null || filter(item.Object)) {
                                if (result.Object == null) {
                                    result = item.Detach();
                                } else {
                                    Debug.Fail("Found more than one symbol named '" + name + "' and matching the filter.");
                                    throw new ArgumentException();
                                }
                            }
                        }
                    }
                } catch {
                    result.Dispose();
                    throw;
                }
            }

            return result;
        }

        public static ComPtr<IDiaSymbol> GetTypeSymbol(this IDiaSymbol moduleSym, string name) {
            IDiaEnumSymbols enumSymbols = null;
            moduleSym.findChildren(SymTagEnum.SymTagUDT, name, 1, out enumSymbols);
            using (ComPtr.Create(enumSymbols)) {
                if (enumSymbols.count > 0) {
                    return ComPtr.Create(enumSymbols.Item(0));
                }
            }

            moduleSym.findChildren(SymTagEnum.SymTagTypedef, name, 1, out enumSymbols);
            using (ComPtr.Create(enumSymbols)) {
                if (enumSymbols.count > 0) {
                    using (var item = ComPtr.Create(enumSymbols.Item(0))) {
                        return ComPtr.Create(item.Object.type);
                    }
                }

                Debug.Fail("Type symbol '" + name + "' was not found.");
                throw new ArgumentException();
            }
        }

        public static long GetFieldOffset(this IDiaSymbol structSym, string name) {
            using (var fieldSym = structSym.GetSymbol(SymTagEnum.SymTagData, name)) {
                return fieldSym.Object.offset;
            }
        }

        public static DkmNativeInstructionAddress GetFunctionAddress(this IDiaSymbol moduleSym, string name, DkmNativeModuleInstance moduleInstance) {
            using (var funSym = moduleSym.GetSymbol(SymTagEnum.SymTagFunction, name)) {
                return DkmNativeInstructionAddress.Create(moduleInstance.Process.GetNativeRuntimeInstance(), moduleInstance, funSym.Object.relativeVirtualAddress, null);
            }
        }
    }
}
