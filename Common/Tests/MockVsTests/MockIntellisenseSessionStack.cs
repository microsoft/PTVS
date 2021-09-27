// Visual Studio Shared Project
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

namespace PythonToolsMockTests
{
    class MockIntellisenseSessionStack : IIntellisenseSessionStack
    {
        private readonly ObservableCollection<IIntellisenseSession> _stack = new ObservableCollection<IIntellisenseSession>();

        public void CollapseAllSessions()
        {
            _stack.Clear();
        }

        public void MoveSessionToTop(IIntellisenseSession session)
        {
            if (!_stack.Remove(session))
            {
                throw new InvalidOperationException();
            }
            PushSession(session);
        }

        public IIntellisenseSession PopSession()
        {
            if (_stack.Count == 0)
            {
                throw new InvalidOperationException();
            }
            var last = _stack[_stack.Count - 1];
            _stack.RemoveAt(_stack.Count - 1);
            return last;
        }

        public void PushSession(IIntellisenseSession session)
        {
            session.Dismissed += session_Dismissed;
            _stack.Add(session);
        }

        void session_Dismissed(object sender, EventArgs e)
        {
            var session = (IIntellisenseSession)sender;
            if (session == TopSession)
            {
                session.Dismissed -= session_Dismissed;
                PopSession();
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public ReadOnlyObservableCollection<IIntellisenseSession> Sessions
        {
            get
            {
                return new ReadOnlyObservableCollection<IIntellisenseSession>(_stack);
            }
        }

        public IIntellisenseSession TopSession
        {
            get
            {
                if (_stack.Count == 0)
                {
                    return null;
                }
                return _stack[_stack.Count - 1];
            }
        }
    }
}
