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

namespace Microsoft.PythonTools.Interpreter {
    public abstract class InterpreterArchitecture : 
        IFormattable,
        IComparable<InterpreterArchitecture>,
        IEquatable<InterpreterArchitecture>
    {
        protected abstract bool Equals(string value);

        public virtual string ToString(string format, IFormatProvider formatProvider, string defaultString) {
            return defaultString;
        }

        public static readonly InterpreterArchitecture Unknown = new UnknownArchitecture();
        public static readonly InterpreterArchitecture x86 = new X86Architecture();
        public static readonly InterpreterArchitecture x64 = new X64Architecture();

        public override string ToString() => ToString(null, null, "");
        public string ToString(string format) => ToString(format, null, "");
        public string ToString(string format, IFormatProvider formatProvider) => ToString(format, formatProvider, "");

        public string ToPEP514() {
            return ToString("PEP514", null);
        }

        public static bool TryParse(string value, out InterpreterArchitecture arch) {
            arch = Unknown;
            if (string.IsNullOrEmpty(value)) {
                return false;
            }

            if (x86.Equals(value)) {
                arch = x86;
                return true;
            } else if (x64.Equals(value)) {
                arch = x64;
                return true;
            }

            return false;
        }

        public static InterpreterArchitecture TryParse(string value) {
            InterpreterArchitecture result;
            if (!TryParse(value, out result)) {
                return Unknown;
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

        public int CompareTo(InterpreterArchitecture other) {
            // We implement the full comparison here rather than delegating to
            // subclasses so that we have some way to handle extra
            // architectures being injected while ensuring that the
            // standard ones take priority.
            
            // The ordering is:
            //      x86
            //      x64
            //      anything else sorted by type name
            //      Unknown

            if (GetType().IsEquivalentTo(other.GetType())) {
                return 0;
            }

            if (this is X86Architecture) {
                return -1;
            }
            if (this is X64Architecture) {
                if (other is X86Architecture) {
                    return 1;
                }
                return -1;
            }
            if (this is UnknownArchitecture) {
                return 1;
            }
            if (other is UnknownArchitecture) {
                return -1;
            }

            return string.CompareOrdinal(GetType().Name, other.GetType().Name);
        }

        public static bool operator ==(InterpreterArchitecture x, InterpreterArchitecture y)
            => x?.Equals(y) ?? object.ReferenceEquals(y, null);
        public static bool operator !=(InterpreterArchitecture x, InterpreterArchitecture y)
            => !(x?.Equals(y) ?? object.ReferenceEquals(y, null));
        public override bool Equals(object obj) => Equals(obj as InterpreterArchitecture);
        public bool Equals(InterpreterArchitecture other) => other != null && GetType().IsEquivalentTo(other.GetType());
        public override int GetHashCode() => GetType().GetHashCode();

        private sealed class UnknownArchitecture : InterpreterArchitecture {
            public UnknownArchitecture() { }
            protected override bool Equals(string value) => false;
        }

        private sealed class X86Architecture : InterpreterArchitecture {
            public X86Architecture() { }

            public override string ToString(string format, IFormatProvider formatProvider, string defaultString) {
                switch (format ?? "") {
                    case "PEP514":
                        return "32bit";
                    case "()":
                        return "(32-bit)";
                    case " ()":
                        return " (32-bit)";
                    case "x":
                        return "x86";
                    case "X":
                        return "X86";
                    case "py":
                        return "win32";
                    case "#":
                        return "32";
                    default:
                        return "32-bit";
                }
            }

            protected override bool Equals(string value) {
                switch (value.ToLowerInvariant().Trim()) {
                    case "32bit":
                    case "32-bit":
                    case "(32-bit)":
                    case "x86":
                        return true;
                }
                return false;
            }
        }

        private sealed class X64Architecture : InterpreterArchitecture {
            public X64Architecture() { }

            public override string ToString(string format, IFormatProvider formatProvider, string defaultString) {
                switch (format ?? "") {
                    case "PEP514":
                        return "64bit";
                    case "()":
                        return "(64-bit)";
                    case " ()":
                        return " (64-bit)";
                    case "x":
                        return "x64";
                    case "X":
                        return "X64";
                    case "py":
                        return "amd64";
                    case "#":
                        return "64";
                    default:
                        return "64-bit";
                }
            }

            protected override bool Equals(string value) {
                switch (value.ToLowerInvariant().Trim()) {
                    case "64bit":
                    case "64-bit":
                    case "(64-bit)":
                    case "amd64":
                        return true;
                }
                return false;
            }
        }
    }
}
