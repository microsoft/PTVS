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
using System.IO;
using System.Linq;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis.Browser {
    class NullMember : IAnalysisItemView {
        public NullMember(string name) {
            Name = name;
        }

        public string Name { get; private set; }

        public string SortKey {
            get { return "9"; }
        }

        public string DisplayType {
            get { return "Unresolved or not supported by browser"; }
        }

        public override string ToString() {
            return Name;
        }

        public IEnumerable<IAnalysisItemView> Children {
            get { return Enumerable.Empty<IAnalysisItemView>(); }
        }

        public IEnumerable<IAnalysisItemView> SortedChildren {
            get { return Children; }
        }

        public string SourceLocation {
            get { return string.Empty; }
        }

        public IEnumerable<KeyValuePair<string, object>> Properties {
            get { yield break; }
        }

        public void ExportToTree(
            TextWriter writer,
            string currentIndent,
            string indent,
            out IEnumerable<IAnalysisItemView> exportChildren
        ) {
            writer.WriteLine("{0}Unknown: {1}", currentIndent, Name);
            exportChildren = null;
        }
    }

    
    abstract class MemberView : IAnalysisItemView {
        protected readonly IModuleContext _context;
        readonly IMember _member;
        readonly string _name;
        readonly Lazy<IEnumerable<IAnalysisItemView>> _children;

        static readonly IAnalysisItemView[] EmptyArray = new IAnalysisItemView[0];
        static readonly Dictionary<Tuple<string, IMember>, IAnalysisItemView> _cache =
            new Dictionary<Tuple<string, IMember>, IAnalysisItemView>();

        protected MemberView(IModuleContext context, string name, IMember member) {
            _context = context;
            _name = name;
            _member = member;
            _children = new Lazy<IEnumerable<IAnalysisItemView>>(CalculateChildren);
        }

        private IEnumerable<IAnalysisItemView> CalculateChildren() {
            var memberContainer = _member as IMemberContainer;
            if (memberContainer != null) {
                return memberContainer.GetMemberNames(_context)
                    .Select(name => Make(_context, memberContainer, name))
                    .ToArray();
            } else {
                return EmptyArray;
            }
        }

        public string Name {
            get { return _name; }
        }

        public abstract string SortKey {
            get;
        }

        public abstract string DisplayType {
            get;
        }

        public override string ToString() {
            return Name;
        }

        public virtual IEnumerable<IAnalysisItemView> Children {
            get {
                return _children.Value;
            }
        }

        public IEnumerable<IAnalysisItemView> SortedChildren {
            get { return Children.OrderBy(c => c.SortKey).ThenBy(c => c.Name); }
        }

        public string SourceLocation {
            get {
                var withLoc = _member as ILocatedMember;
                if (withLoc == null || !withLoc.Locations.Any()) {
                    return "No location";
                }
                return string.Join(Environment.NewLine,
                    withLoc.Locations.Select(loc => string.Format("{0}:{1}", loc.FilePath, loc.Line)));
            }
        }

        public virtual IEnumerable<KeyValuePair<string, object>> Properties {
            get {
                yield return new KeyValuePair<string, object>("Locations", SourceLocation);
            }
        }

        public virtual void ExportToTree(
            TextWriter writer,
            string currentIndent,
            string indent,
            out IEnumerable<IAnalysisItemView> exportChildren
        ) {
            writer.WriteLine("{0}{1}: {2}", currentIndent, DisplayType, Name);
            exportChildren = SortedChildren;
        }

        public static IAnalysisItemView Make(IModuleContext context, IMemberContainer members, string name) {
            return Make(context, name, members.GetMember(context, name));
        }

        public static IAnalysisItemView Make(IModuleContext context, string name, IMember member) {
            if (member == null) {
                return new NullMember(name);
            }

            IAnalysisItemView result;
            var key = Tuple.Create(name, member);
            if (_cache.TryGetValue(key, out result)) {
                return result;
            }
            _cache[key] = result = MakeNoCache(context, name, member);
            return result;
        }

        private static IAnalysisItemView MakeNoCache(IModuleContext context, string name, IMember member) {
            switch (member.MemberType) {
                case PythonMemberType.Class:
                    return new ClassView(context, name, (IPythonType)member);
                case PythonMemberType.Instance:
                case PythonMemberType.Constant:
                case PythonMemberType.Field:
                    return new ValueView(context, name, (IPythonConstant)member);
                case PythonMemberType.Function:
                    return new FunctionView(context, name, (IPythonFunction)member, false);
                case PythonMemberType.Method:
                    return new FunctionView(context, name, ((IPythonMethodDescriptor)member).Function, true);
                case PythonMemberType.Property:
                    return new PropertyView(context, name, (IBuiltinProperty)member);
                case PythonMemberType.Module:
                    return new ModuleRefView(context, name, (IPythonModule)member);
                case PythonMemberType.Multiple:
                    return new MultipleMemberView(context, name, (IPythonMultipleMembers)member);
                case PythonMemberType.Unknown:
                case PythonMemberType.Delegate:
                case PythonMemberType.DelegateInstance:
                case PythonMemberType.Enum:
                case PythonMemberType.EnumInstance:
                case PythonMemberType.Namespace:
                case PythonMemberType.Event:
                case PythonMemberType.Keyword:
                default:
                    Debug.WriteLine(string.Format("Cannot display {0} ({1})", name, member.MemberType));
                    break;
            }

            return new NullMember(name);
        }
    }
}
