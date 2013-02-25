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
using System.Xml.Serialization;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools {
    /// <summary>
    /// Provides a view model for the PythonInterpreter class.
    /// </summary>
    class PythonInterpreterView {
        readonly string _name;
        readonly Guid _id;
        readonly Version _version;
        readonly string _path;
        
        /// <summary>
        /// Create a PythonInterpreterView with values from an IPythonInterpreterFactory.
        /// </summary>
        public PythonInterpreterView(IPythonInterpreterFactory factory) {
            _name = factory.GetInterpreterDisplay();
            _id = factory.Id;
            _version = factory.Configuration.Version;
            _path = factory.Configuration.InterpreterPath;
        }

        /// <summary>
        /// Create a PythonInterpreterView with values from a PythonInterpreter.
        /// </summary>
        public PythonInterpreterView(PythonInterpreter interpreter) {
            _name = null;
            _id = interpreter.Id;
            _version = Version.Parse(interpreter.Version);
            _path = null;
        }
        /// <summary>
        /// Create a PythonInterpreterView with values from parameters.
        /// </summary>
        public PythonInterpreterView(string name, Guid id, Version version, string path) {
            _name = name;
            _id = id;
            _version = version;
            _path = path;
        }

        /// <summary>
        /// Returns a PythonInterpreter with the values from the model view.
        /// </summary>
        /// <returns></returns>
        public PythonInterpreter GetInterpreter() {
            return new PythonInterpreter {
                Id = Id,
                Version = Version.ToString()
            };
        }

        /// <summary>
        /// The display name of the interpreter, if available.
        /// </summary>
        public string Name {
            get {
                return _name;
            }
        }

        /// <summary>
        /// The Guid identifying the interpreter.
        /// </summary>
        public Guid Id { 
            get {
                return _id;
            }
        }

        /// <summary>
        /// The version of the interpreter.
        /// </summary>
        public Version Version { 
            get {
                return _version;
            }
        }

        /// <summary>
        /// The path to the interpreter, if available.
        /// </summary>
        public string Path { 
            get {
                return _path;
            }
        }

        public override string ToString() {
            return Name;
        }

        public override bool Equals(object obj) {
            var other = obj as PythonInterpreterView;
            if (other == null) {
                return false;
            } else {
                return Id.Equals(other.Id) && Version.Equals(other.Version);
            }
        }

        public override int GetHashCode() {
            return Id.GetHashCode();
        }
    }

    sealed class PythonInterpreter {
        [XmlElement("Id")]
        public Guid Id {
            get;
            set;
        }

        [XmlElement("Version")]
        public string Version {
            get;
            set;
        }

        internal PythonInterpreter Clone() {
            var res = new PythonInterpreter();

            res.Id = Id;
            res.Version = Version;
            return res;
        }

        internal static bool IsSame(PythonInterpreter self, PythonInterpreter other) {
            if (self == null) {
                return other == null;
            } else if (other != null) {
                return self.Id == other.Id &&
                    self.Version == other.Version;
            }
            return false;
        }
    }
}
 
