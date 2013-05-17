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
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis {
    /// <summary>
    /// Base class for all analysis values.  Exposes the public API of the analysis engine.
    /// 
    /// New in 1.5.
    /// </summary>
    public class AnalysisValue {
        internal AnalysisValue() {
        }

        /// <summary>
        /// Gets the name of the value if it has one, or null if it's a non-named item.
        /// 
        /// The name property here is typically the same value you'd get by accessing __name__
        /// on the real Python object.
        /// </summary>
        public virtual string Name {
            get {
                return null;
            }
        }

        /// <summary>
        /// Returns the member type of the analysis value, or PythonMemberType.Unknown if it's unknown.
        /// </summary>
        public virtual PythonMemberType MemberType {
            get {
                return PythonMemberType.Unknown;
            }
        }

        /// <summary>
        /// Returns the documentation for the object, or String.Empty if no documentation is available.
        /// </summary>
        public virtual string Documentation {
            get {
                return String.Empty;
            }
        }

        /// <summary>
        /// Gets a list of locations where this value is defined.
        /// </summary>
        public virtual IEnumerable<LocationInfo> Locations {
            get { return LocationInfo.Empty; }
        }

        /// <summary>
        /// Gets the constant value that this object represents, if it's a constant.
        /// 
        /// Returns Type.Missing if the value is not constant (because it returns null
        /// if the type is None).
        /// </summary>
        /// <returns></returns>
        public virtual object GetConstantValue() {
            return Type.Missing;
        }

        /// <summary>
        /// Returns the constant value as a string.  This returns a string if the constant
        /// value is either a unicode or ASCII string.
        /// </summary>
        public string GetConstantValueAsString() {
            var constName = GetConstantValue();
            if (constName != null) {
                string unicodeName = constName as string;
                AsciiString asciiName;
                if (unicodeName != null) {
                    return unicodeName;
                } else if ((asciiName = constName as AsciiString) != null) {
                    return asciiName.String;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns a list of key/value pairs stored in the this object which are retrivable using
        /// indexing.  For lists the key values will be integers (potentially constant, potentially not), 
        /// for dicts the key values will be arbitrary analysis values.
        /// </summary>
        /// <returns></returns>
        public virtual IEnumerable<KeyValuePair<IEnumerable<AnalysisValue>, IEnumerable<AnalysisValue>>> GetItems() {
            yield break;
        }

        public virtual IDictionary<string, ISet<AnalysisValue>> GetAllMembers() {
            return new Dictionary<string, ISet<AnalysisValue>>();
        }

        internal virtual Namespace AsNamespace() {
            throw new InvalidOperationException();
        }
    }

    /// <summary>
    /// Provides an external analysis value which can be returned from specialized
    /// calls.  Consumers can subclass and override GetMember to participate in the 
    /// analysis further. Or if you just need to hold onto some data you can use 
    /// ExternalAnalysisValue&lt;T&gt; 
    /// 
    /// New in 1.5.
    /// </summary>
    public class ExternalAnalysisValue : AnalysisValue {
        private readonly ExternalNamespace _ns;

        public ExternalAnalysisValue() {
            _ns = new ExternalNamespace(this);
        }

        internal ExternalNamespace Namespace {
            get {
                return _ns;
            }
        }

        public virtual IEnumerable<AnalysisValue> GetMember(string name) {
            return EmptySet<AnalysisValue>.Instance;
        }

        public virtual IEnumerable<AnalysisValue> Call(ISet<AnalysisValue>[] args, NameExpression[] keywordArgNames) {
            return EmptySet<AnalysisValue>.Instance;
        }

        internal override Namespace AsNamespace() {
            return _ns;
        }
    }

    /// <summary>
    /// Provides an external analysis value which can be returned from specialized calls
    /// and then processed when received later from another specialized call.
    /// 
    /// This enables extending the analysis system with your own values.
    /// 
    /// New in 1.5.
    /// </summary>
    public class ExternalAnalysisValue<T> : ExternalAnalysisValue {
        private readonly T _data;

        public ExternalAnalysisValue(T data) {
            _data = data;
        }

        public T Data {
            get {
                return _data;
            }
        }
    }

    class ExternalNamespace : Namespace {
        private readonly ExternalAnalysisValue _value;

        public ExternalNamespace(ExternalAnalysisValue value) {
            _value = value;
        }

        public ExternalAnalysisValue Value {
            get {
                return _value;
            }
        }

        public override INamespaceSet GetMember(Node node, AnalysisUnit unit, string name) {
            return ToNamespaceSet(_value.GetMember(name));
        }

        /// <summary>
        /// Returns the member type of the analysis value, if it's known.
        /// </summary>
        public override PythonMemberType MemberType {
            get {
                return _value.MemberType;
            }
        }

        public override INamespaceSet Call(Node node, AnalysisUnit unit, INamespaceSet[] args, NameExpression[] keywordArgNames) {
            ISet<AnalysisValue>[] extArgs = new ISet<AnalysisValue>[args.Length];
            for (int i = 0; i < args.Length; i++) {
                HashSet<AnalysisValue> set;
                extArgs[i] = set = new HashSet<AnalysisValue>();
                foreach (var argValue in args[i]) {
                    set.Add(argValue.AsExternal());
                }
            }

            return ToNamespaceSet(_value.Call(extArgs, keywordArgNames));
        }

        private INamespaceSet ToNamespaceSet(IEnumerable<AnalysisValue> iEnumerable) {
            var res = NamespaceSet.Empty;
            foreach (var value in iEnumerable) {
                res.Add(value.AsNamespace());
            }
            return res;
        }

        internal override AnalysisValue AsExternal() {
            return _value;
        }
    }
}
