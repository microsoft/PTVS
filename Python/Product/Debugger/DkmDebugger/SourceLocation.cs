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
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Native;

namespace Microsoft.PythonTools.DkmDebugger {
    internal class SourceLocation : DkmDataItem, IEquatable<SourceLocation> {
        public string FileName { get; private set; }
        public string FunctionName { get; private set; }
        public int LineNumber { get; private set; }
        public DkmNativeInstructionAddress NativeAddress { get; private set; }

        public SourceLocation(string fileName, int lineNumber, string functionName = null, DkmNativeInstructionAddress nativeAddress = null) {
            FileName = fileName;
            LineNumber = lineNumber;
            FunctionName = functionName;
            NativeAddress = nativeAddress;
        }

        public SourceLocation(ReadOnlyCollection<byte> encodedLocation, DkmProcess process = null) {
            var buffer = encodedLocation.ToArray();
            using (var stream = new MemoryStream(buffer))
            using (var reader = new BinaryReader(stream)) {
                FileName = reader.ReadString();
                bool hasFunctionName = reader.ReadBoolean();
                if (hasFunctionName) {
                    FunctionName = reader.ReadString();
                }
                LineNumber = reader.ReadInt32();
                bool hasNativeAddress = reader.ReadBoolean();
                if (hasNativeAddress && process != null) {
                    var ip = reader.ReadUInt64();
                    var rva = reader.ReadUInt32();

                    NativeAddress = DkmNativeInstructionAddress.Create(
                        process.GetNativeRuntimeInstance(),
                        process.GetPythonRuntimeInfo().DLLs.Python,
                        rva,
                        new DkmNativeInstructionAddress.CPUInstruction(ip));
                } else {
                    NativeAddress = null;
                }
            }
        }

        public ReadOnlyCollection<byte> Encode() {
            using (var stream = new MemoryStream()) {
                using (var writer = new BinaryWriter(stream)) {
                    writer.Write(FileName);
                    if (FunctionName != null) {
                        writer.Write(true);
                        writer.Write(FunctionName);
                    } else {
                        writer.Write(false);
                    }
                    writer.Write(LineNumber);
                    if (NativeAddress != null) {
                        writer.Write(true);
                        writer.Write(NativeAddress.CPUInstructionPart.InstructionPointer);
                        writer.Write(NativeAddress.RVA);
                    } else {
                        writer.Write(false);
                    }
                }

                return new ReadOnlyCollection<byte>(stream.ToArray());
            }
        }

        public bool Equals(SourceLocation other) {
            return FileName == other.FileName && LineNumber == other.LineNumber && FunctionName == other.FunctionName && NativeAddress == other.NativeAddress;
        }

        public override bool Equals(object obj) {
            var other = obj as SourceLocation;
            return other == null ? false : Equals(other);
        }

        public override int GetHashCode() {
            return new { FileName, LineNumber, FunctionName, NativeAddress }.GetHashCode();
        }
    }
}
