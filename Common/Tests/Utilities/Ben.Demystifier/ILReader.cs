// Copyright (c) Ben A Adams. All rights reserved.
// Licensed under the Apache License, Version 2.0.
// 
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

using System;
using System.Reflection;
using System.Reflection.Emit;

namespace TestUtilities.Ben.Demystifier {
    internal sealed class ILReader {
        private static readonly OpCode[] singleByteOpCode;
        private static readonly OpCode[] doubleByteOpCode;

        private readonly byte[] _cil;
        private int _ptr;

        public ILReader(byte[] cil) => _cil = cil;

        public OpCode OpCode { get; private set; }
        public int MetadataToken { get; private set; }
        public MemberInfo Operand { get; private set; }

        public bool Read(MethodBase methodInfo) {
            if (_ptr < _cil.Length) {
                OpCode = ReadOpCode();
                Operand = ReadOperand(OpCode, methodInfo);
                return true;
            }
            return false;
        }

        private OpCode ReadOpCode() {
            var instruction = ReadByte();
            return instruction < 254 ? singleByteOpCode[instruction] : doubleByteOpCode[ReadByte()];
        }

        private MemberInfo ReadOperand(OpCode code, MethodBase methodInfo) {
            MetadataToken = 0;
            switch (code.OperandType) {
                case OperandType.InlineMethod:
                    MetadataToken = ReadInt();
                    Type[] methodArgs = null;
                    if (methodInfo.GetType() != typeof(ConstructorInfo) && !methodInfo.GetType().IsSubclassOf(typeof(ConstructorInfo))) {
                        methodArgs = methodInfo.GetGenericArguments();
                    }

                    Type[] typeArgs = null;
                    if (methodInfo.DeclaringType != null) {
                        typeArgs = methodInfo.DeclaringType.GetGenericArguments();
                    }

                    try {
                        return methodInfo.Module.ResolveMember(MetadataToken, typeArgs, methodArgs);
                    } catch {
                        // Can return System.ArgumentException : Token xxx is not a valid MemberInfo token in the scope of module xxx.dll
                        return null;
                    }
                default:
                    return null;
            }
        }

        private byte ReadByte() => _cil[_ptr++];

        private int ReadInt() {
            var b1 = ReadByte();
            var b2 = ReadByte();
            var b3 = ReadByte();
            var b4 = ReadByte();
            return b1 | b2 << 8 | b3 << 16 | b4 << 24;
        }

        static ILReader(){
            singleByteOpCode = new OpCode[225];
            doubleByteOpCode = new OpCode[31];

            var fields = GetOpCodeFields();

            foreach (var field in fields) {
                var code = (OpCode)field.GetValue(null);
                if (code.OpCodeType == OpCodeType.Nternal)
                    continue;

                if (code.Size == 1)
                    singleByteOpCode[code.Value] = code;
                else
                    doubleByteOpCode[code.Value & 0xff] = code;
            }
        }

        private static FieldInfo[] GetOpCodeFields() => typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static);
    }
}
