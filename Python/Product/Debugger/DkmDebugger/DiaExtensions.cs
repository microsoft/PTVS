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
using Microsoft.VisualStudio.Debugger;
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
        public static IEnumerable<IDiaSymbol> GetSymbols(this IDiaSymbol symbol, SymTagEnum symTag, string name) {
            IDiaEnumSymbols enumSymbols;
            symbol.findChildren(symTag, name, 1, out enumSymbols);
            int n = enumSymbols.count;
            for (int i = 0; i < n; ++i) {
                yield return enumSymbols.Item((uint)i);
            }
        }

        public static IDiaSymbol GetSymbol(this IDiaSymbol symbol, SymTagEnum symTag, string name) {
            var result = GetSymbols(symbol, symTag, name).ToArray();
            if (result.Length == 1) {
                return result[0];
            } else if (result.Length == 0) {
                Debug.Fail("Symbol '" + name + "' was not found.");
                throw new ArgumentException();
            } else {
                Debug.Fail("Found more than one symbol named '" + name + "'.");
                throw new ArgumentException();
            }
        }

        public static IDiaSymbol GetTypeSymbol(this IDiaSymbol moduleSym, string name) {
            IDiaEnumSymbols enumSymbols;
            moduleSym.findChildren(SymTagEnum.SymTagUDT, name, 1, out enumSymbols);
            if (enumSymbols.count > 0) {
                return enumSymbols.Item(0);
            }

            moduleSym.findChildren(SymTagEnum.SymTagTypedef, name, 1, out enumSymbols);
            if (enumSymbols.count > 0) {
                return enumSymbols.Item(0).type;
            }

            Debug.Fail("Type symbol '" + name + "' was not found.");
            throw new ArgumentException();
        }

        public static long GetFieldOffset(this IDiaSymbol structSym, string name) {
            return structSym.GetSymbol(SymTagEnum.SymTagData, name).offset;
        }

        public static DkmNativeInstructionAddress GetFunctionAddress(this IDiaSymbol moduleSym, string name, DkmNativeModuleInstance moduleInstance) {
            IDiaSymbol sym = moduleSym.GetSymbol(SymTagEnum.SymTagFunction, name);
            return DkmNativeInstructionAddress.Create(moduleInstance.Process.GetNativeRuntimeInstance(), moduleInstance, sym.relativeVirtualAddress, null);
        }
    }
}
