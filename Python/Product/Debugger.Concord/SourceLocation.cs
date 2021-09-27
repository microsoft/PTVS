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
    internal class SourceLocation : DkmDataItem, IEquatable<SourceLocation>
    {
        public string FileName { get; private set; }
        public string FunctionName { get; private set; }
        public int LineNumber { get; private set; }
        public DkmNativeInstructionAddress NativeAddress { get; private set; }

        public SourceLocation(string fileName, int lineNumber, string functionName = null, DkmNativeInstructionAddress nativeAddress = null)
        {
            FileName = fileName;
            LineNumber = lineNumber;
            FunctionName = functionName;
            NativeAddress = nativeAddress;
        }

        public SourceLocation(ReadOnlyCollection<byte> encodedLocation, DkmProcess process = null)
        {
            var buffer = encodedLocation.ToArray();
            using (var stream = new MemoryStream(buffer))
            using (var reader = new BinaryReader(stream))
            {
                FileName = reader.ReadString();
                bool hasFunctionName = reader.ReadBoolean();
                if (hasFunctionName)
                {
                    FunctionName = reader.ReadString();
                }
                LineNumber = reader.ReadInt32();
                bool hasNativeAddress = reader.ReadBoolean();
                if (hasNativeAddress && process != null)
                {
                    var ip = reader.ReadUInt64();
                    var rva = reader.ReadUInt32();

                    var dlls = process.GetPythonRuntimeInfo().DLLs;
                    DkmNativeModuleInstance dll = null;
                    switch (reader.ReadInt32())
                    {
                        case 0:
                            dll = dlls.Python;
                            break;
                        case 1:
                            dll = dlls.DebuggerHelper;
                            break;
                    }

                    if (dll != null)
                    {
                        NativeAddress = DkmNativeInstructionAddress.Create(
                            process.GetNativeRuntimeInstance(),
                            dll,
                            rva,
                            new DkmNativeInstructionAddress.CPUInstruction(ip));
                    }
                }
                else
                {
                    NativeAddress = null;
                }
            }
        }

        public ReadOnlyCollection<byte> Encode()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(FileName);
                if (FunctionName != null)
                {
                    writer.Write(true);
                    writer.Write(FunctionName);
                }
                else
                {
                    writer.Write(false);
                }
                writer.Write(LineNumber);
                if (NativeAddress != null)
                {
                    writer.Write(true);
                    writer.Write(NativeAddress.CPUInstructionPart.InstructionPointer);
                    writer.Write(NativeAddress.RVA);
                    var dlls = NativeAddress.Process.GetPythonRuntimeInfo().DLLs;
                    if (NativeAddress.ModuleInstance == dlls.Python)
                    {
                        writer.Write(0);
                    }
                    else if (NativeAddress.ModuleInstance == dlls.DebuggerHelper)
                    {
                        writer.Write(1);
                    }
                }
                else
                {
                    writer.Write(false);
                }
                writer.Flush();
                return new ReadOnlyCollection<byte>(stream.ToArray());
            }
        }

        public bool Equals(SourceLocation other)
        {
            return FileName == other.FileName && LineNumber == other.LineNumber && FunctionName == other.FunctionName && NativeAddress == other.NativeAddress;
        }

        public override bool Equals(object obj)
        {
            var other = obj as SourceLocation;
            return other == null ? false : Equals(other);
        }

        public override int GetHashCode()
        {
            return new { FileName, LineNumber, FunctionName, NativeAddress }.GetHashCode();
        }
    }
}
