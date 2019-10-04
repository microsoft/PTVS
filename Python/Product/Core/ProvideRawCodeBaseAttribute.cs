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
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.PythonTools.Core {
    /// <summary>
    /// Registers a set of values that define a dependent assembly tag with a code base.
    /// </summary>
    /// <remarks>
    /// This differs from the standard ProvideCodeBaseAttribute class in that it does
    /// not load the assembly when doing registration, so all fields must be supplied.
    /// </remarks>

    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = true)]
    internal sealed class ProvideRawCodeBaseAttribute : RegistrationAttribute {
        private Guid? _guid;

        public ProvideRawCodeBaseAttribute() {
        }

        /// <summary>
        /// Gets the identifier of this attribute instance.
        /// </summary>
        public Guid Guid {
            get {
                if (!_guid.HasValue) {
                    CalculateGuid();
                }

                return _guid.Value;
            }

            set {
                _guid = value;
            }
        }

        /// <summary>
        /// Gets or sets the name of the target assembly.
        /// </summary>
        public string AssemblyName { get; set; }

        /// <summary>
        /// Gets or sets a 16-character hexadecimal number which is the token
        /// part of the strong name of the assembly being redirected.
        /// </summary>
        public string PublicKeyToken { get; set; }

        /// <summary>
        /// Gets or sets a string that specifies the language and
        /// country/region of the assembly.
        /// </summary>
        public string Culture { get; set; }

        /// <summary>
        /// Gets or sets the version of the assembly to use instead of the
        /// originally-requested version.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets a path to the assembly file.
        /// </summary>
        /// <remarks>
        /// Differs from the standard ProvdeCodeBaseAttribute in that you must
        /// include the $PackageFolder$ for a relative path.
        /// </remarks>
        public string CodeBase { get; set; }

        public override void Register(RegistrationContext context) {
            string keyPath = string.Format(
                CultureInfo.InvariantCulture,
                "RuntimeConfiguration\\dependentAssembly\\codeBase\\{0}",
                Guid.ToString("B").ToUpperInvariant()
            );

            using (var key = context.CreateKey(keyPath)) {
                key.SetValue(ValueNames.Name, AssemblyName);
                key.SetValue(ValueNames.PublicKeyToken, PublicKeyToken);
                key.SetValue(ValueNames.Culture, Culture);
                key.SetValue(ValueNames.Version, Version);
                key.SetValue(ValueNames.CodeBase, CodeBase);
            }
        }

        public override void Unregister(RegistrationContext context) {
        }

        void CalculateGuid() {
            // Maintain a stable attribute guid in every build, when assembly info is the same.
            // Logic is the same as used in ProvideCodeBaseAttribute class.
            using (var sha2 = SHA256.Create()) {
                var strongName = string.Format("{0},{1},{2},{3}", AssemblyName, PublicKeyToken, Culture, Version);
                var strongNameBytes = Encoding.UTF8.GetBytes(strongName);
                var fullHash = sha2.ComputeHash(strongNameBytes);
                var targetBlockSize = Marshal.SizeOf(typeof(Guid));
                var reducedHash = fullHash.Take(targetBlockSize).Zip(fullHash.Skip(targetBlockSize), (b1, b2) => (byte)(b1 ^ b2)).ToArray();
                Debug.Assert(reducedHash.Length == targetBlockSize, "The size of our combined hash block is not == sizeof(Guid).");

                Guid = new Guid(reducedHash);
            }
        }

        private static class ValueNames {
            public static readonly string Name = "name";
            public static readonly string PublicKeyToken = "publicKeyToken";
            public static readonly string Culture = "culture";
            public static readonly string CodeBase = "codeBase";
            public static readonly string Version = "version";
        }
    }
}