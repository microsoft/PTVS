using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstPythonMultipleMembers : IPythonMultipleMembers, ILocatedMember {
        private IList<IMember> _members;
        private bool _checkForLazy;

        public AstPythonMultipleMembers() {
            _members = Array.Empty<IMember>();
        }

        private AstPythonMultipleMembers(IMember[] members) {
            _members = members;
            _checkForLazy = true;
        }

        public AstPythonMultipleMembers(IEnumerable<IMember> members) {
            _members = members.ToArray();
            _checkForLazy = true;
        }

        public static IMember Combine(IMember x, IMember y) {
            if (x == null && y == null) {
                throw new InvalidOperationException("Cannot add two null members");
            } else if (x == null || (x.MemberType == PythonMemberType.Unknown && !(x is ILazyMember))) {
                return y;
            } else if (y == null || (y.MemberType == PythonMemberType.Unknown && !(y is ILazyMember))) {
                return x;
            } else if (x == y) {
                return x;
            }

            var mmx = x as AstPythonMultipleMembers;
            var mmy = y as AstPythonMultipleMembers;

            if (mmx != null && mmy == null) {
                mmx.AddMember(y);
                return mmx;
            } else if (mmy != null && mmx == null) {
                mmy.AddMember(x);
                return mmy;
            } else if (mmx != null && mmy != null) {
                mmx.AddMembers(mmy._members);
                return mmx;
            } else {
                return new AstPythonMultipleMembers(new[] { x, y });
            }
        }

        public void AddMember(IMember member) {
            var old = _members;
            if (!old.Contains(member)) {
                _members = old.Concat(Enumerable.Repeat(member, 1)).ToArray();
                _checkForLazy = true;
            } else if (!old.Any()) {
                _members = new[] { member };
                _checkForLazy = true;
            }
        }

        public void AddMembers(IEnumerable<IMember> members) {
            var old = _members;
            if (old.Any()) {
                _members = old.Union(members).ToArray();
                _checkForLazy = true;
            } else {
                _members = members.ToArray();
                _checkForLazy = true;
            }
        }


        public IList<IMember> Members {
            get {
                if (_checkForLazy) {
                    _members = _members.Select(m => (m as ILazyMember)?.Get() ?? m).ToArray();
                    _checkForLazy = false;
                }
                return _members;
            }
        }

        public PythonMemberType MemberType => PythonMemberType.Multiple;

        public IEnumerable<LocationInfo> Locations => _members.OfType<ILocatedMember>().SelectMany(m => m.Locations.MaybeEnumerate());
    }
}
