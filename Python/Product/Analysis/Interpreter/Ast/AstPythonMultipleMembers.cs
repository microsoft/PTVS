using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstPythonMultipleMembers : IPythonMultipleMembers {
        public AstPythonMultipleMembers() {
            Members = Array.Empty<IMember>();
        }

        public AstPythonMultipleMembers(IEnumerable<IMember> members) {
            Members = members.ToArray();
        }

        public static IMember Combine(IMember x, IMember y) {
            if (x == null && y == null) {
                throw new InvalidOperationException("Cannot add two null members");
            } else if (x == null || x.MemberType == PythonMemberType.Unknown) {
                return y;
            } else if (y == null || y.MemberType == PythonMemberType.Unknown) {
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
                mmx.AddMembers(mmy.Members);
                return mmx;
            } else {
                return new AstPythonMultipleMembers(new[] { x, y });
            }
        }

        public void AddMember(IMember member) {
            var old = Members;
            if (!old.Contains(member)) {
                Members = old.Concat(Enumerable.Repeat(member, 1)).ToArray();
            } else if (!old.Any()) {
                Members = new[] { member };
            }
        }

        public void AddMembers(IEnumerable<IMember> members) {
            var old = Members;
            if (old.Any()) {
                Members = old.Union(members).ToArray();
            } else {
                Members = members.ToArray();
            }
        }

        public IList<IMember> Members { get; private set; }

        public PythonMemberType MemberType => PythonMemberType.Multiple;
    }
}
