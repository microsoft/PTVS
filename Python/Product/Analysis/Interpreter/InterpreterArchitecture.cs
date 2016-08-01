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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Reflection;

namespace Microsoft.PythonTools.Interpreter {
    public struct InterpreterArchitecture : 
        IFormattable,
        IEquatable<InterpreterArchitecture>,
        IComparable<InterpreterArchitecture>
    {
        public ProcessorArchitecture Value { get; set; }

        public InterpreterArchitecture(ProcessorArchitecture arch) {
            Value = arch;
        }

        public static readonly InterpreterArchitecture Unknown = new InterpreterArchitecture(ProcessorArchitecture.None);
        public static readonly InterpreterArchitecture x86 = new InterpreterArchitecture(ProcessorArchitecture.X86);
        public static readonly InterpreterArchitecture x64 = new InterpreterArchitecture(ProcessorArchitecture.Amd64);

        public static bool operator ==(InterpreterArchitecture x, InterpreterArchitecture y) {
            return x.Value == y.Value;
        }

        public static bool operator !=(InterpreterArchitecture x, InterpreterArchitecture y) {
            return x.Value != y.Value;
        }

        public override bool Equals(object obj) {
            if (!(obj is InterpreterArchitecture)) {
                return false;
            }
            return Value.Equals(((InterpreterArchitecture)obj).Value);
        }

        public override int GetHashCode() {
            return Value.GetHashCode();
        }

        public override string ToString() => ToString(null, null, "");
        public string ToString(string format) => ToString(format, null, "");
        public string ToString(string format, IFormatProvider formatProvider) => ToString(format, formatProvider, "");

        public string ToPEP514() {
            return ToString("PEP514", null);
        }

        public string ToString(string format, IFormatProvider formatProvider, string defaultString) {
            switch (format ?? "") {
                case "PEP514":
                    switch (Value) {
                        case ProcessorArchitecture.X86:
                            return "32bit";
                        case ProcessorArchitecture.Amd64:
                            return "64bit";
                    }
                    break;
                case "()":
                    switch (Value) {
                        case ProcessorArchitecture.X86:
                            return "(32-bit)";
                        case ProcessorArchitecture.Amd64:
                            return "(64-bit)";
                    }
                    break;
                case " ()":
                    switch (Value) {
                        case ProcessorArchitecture.X86:
                            return " (32-bit)";
                        case ProcessorArchitecture.Amd64:
                            return " (64-bit)";
                    }
                    break;
                case "x":
                    switch (Value) {
                        case ProcessorArchitecture.X86:
                            return "x86";
                        case ProcessorArchitecture.Amd64:
                            return "x64";
                    }
                    break;
                case "X":
                    switch (Value) {
                        case ProcessorArchitecture.X86:
                            return "X86";
                        case ProcessorArchitecture.Amd64:
                            return "X64";
                    }
                    break;
                case "py":
                    switch (Value) {
                        case ProcessorArchitecture.X86:
                            return "win32";
                        case ProcessorArchitecture.Amd64:
                            return "amd64";
                    }
                    break;
                case "#":
                    switch (Value) {
                        case ProcessorArchitecture.X86:
                            return "32";
                        case ProcessorArchitecture.Amd64:
                            return "64";
                    }
                    break;
                default:
                    switch (Value) {
                        case ProcessorArchitecture.X86:
                            return "32-bit";
                        case ProcessorArchitecture.Amd64:
                            return "64-bit";
                    }
                    break;
            }
            return defaultString;
        }

        public static bool TryParse(string value, out InterpreterArchitecture arch) {
            arch = Unknown;
            if (string.IsNullOrEmpty(value)) {
                return false;
            }

            switch (value.ToLowerInvariant().Trim()) {
                case "32bit":
                case "32-bit":
                case "(32-bit)":
                case "x86":
                    arch = x86;
                    break;
                case "64bit":
                case "64-bit":
                case "(64-bit)":
                case "x64":
                case "amd64":
                    arch = x64;
                    break;
                default:
                    return false;
            }

            return true;
        }

        public static InterpreterArchitecture TryParse(string value) {
            InterpreterArchitecture result;
            if (!TryParse(value, out result)) {
                return InterpreterArchitecture.Unknown;
            }
            return result;
        }

        public static InterpreterArchitecture Parse(string value) {
            InterpreterArchitecture result;
            if (!TryParse(value, out result)) {
                throw new FormatException();
            }
            return result;
        }

        public bool Equals(InterpreterArchitecture other) {
            return Value == other.Value;
        }

        public int CompareTo(InterpreterArchitecture other) {
            return Value.CompareTo(other.Value);
        }
    }
}
