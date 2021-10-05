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
	internal enum DiaLocationType : uint
	{
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

	internal static class DiaExtensions
	{
		public static ComPtr<IDiaSymbol>[] GetSymbols(this IDiaSymbol symbol, SymTagEnum symTag, string name)
		{
			symbol.findChildren(symTag, name, 1, out IDiaEnumSymbols enumSymbols);
			using (ComPtr.Create(enumSymbols))
			{
				int n = enumSymbols.count;
				var result = new ComPtr<IDiaSymbol>[n];
				try
				{
					for (int i = 0; i < n; ++i)
					{
						result[i] = ComPtr.Create(enumSymbols.Item((uint)i));
					}
				}
				catch
				{
					foreach (var item in result)
					{
						item.Dispose();
					}
					throw;
				}
				return result;
			}
		}

		public static ComPtr<IDiaSymbol> GetSymbol(this IDiaSymbol symbol, SymTagEnum symTag, string name, Predicate<IDiaSymbol> filter = null)
		{
			var result = new ComPtr<IDiaSymbol>();

			symbol.findChildren(symTag, name, 1, out IDiaEnumSymbols enumSymbols);
			using (ComPtr.Create(enumSymbols))
			{
				int n = enumSymbols.count;
				if (n == 0)
				{
					Debug.Fail("Symbol '" + name + "' was not found.");
					throw new ArgumentException();
				}

				try
				{
					for (int i = 0; i < n; ++i)
					{
						using (var item = ComPtr.Create(enumSymbols.Item((uint)i)))
						{
							if (filter == null || filter(item.Object))
							{
								if (result.Object == null)
								{
									result = item.Detach();
								}
								else
								{
									Debug.Fail("Found more than one symbol named '" + name + "' and matching the filter.");
									throw new ArgumentException();
								}
							}
						}
					}
				}
				catch
				{
					result.Dispose();
					throw;
				}
			}

			return result;
		}

		public static ComPtr<IDiaSymbol> GetTypeSymbol(this IDiaSymbol moduleSym, string name)
		{
			moduleSym.findChildren(SymTagEnum.SymTagUDT, name, 1, out IDiaEnumSymbols enumSymbols);
			using (ComPtr.Create(enumSymbols))
			{
				if (enumSymbols.count > 0)
				{
					return ComPtr.Create(enumSymbols.Item(0));
				}
			}

			moduleSym.findChildren(SymTagEnum.SymTagTypedef, name, 1, out enumSymbols);
			using (ComPtr.Create(enumSymbols))
			{
				if (enumSymbols.count > 0)
				{
					using (var item = ComPtr.Create(enumSymbols.Item(0)))
					{
						return ComPtr.Create(item.Object.type);
					}
				}

				Debug.Fail("Type symbol '" + name + "' was not found.");
				throw new ArgumentException();
			}
		}

		public static long GetFieldOffset(this IDiaSymbol structSym, string name)
		{
			using (var fieldSym = structSym.GetSymbol(SymTagEnum.SymTagData, name))
			{
				return fieldSym.Object.offset;
			}
		}

		public static DkmNativeInstructionAddress GetFunctionAddress(this IDiaSymbol moduleSym, string name, DkmNativeModuleInstance moduleInstance)
		{
			using (var funSym = moduleSym.GetSymbol(SymTagEnum.SymTagFunction, name))
			{
				return DkmNativeInstructionAddress.Create(moduleInstance.Process.GetNativeRuntimeInstance(), moduleInstance, funSym.Object.relativeVirtualAddress, null);
			}
		}
	}
}
