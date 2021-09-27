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

namespace Microsoft.VisualStudioTools.MockVsTests
{
    class MockTreeNode : ITreeNode
    {
        private readonly MockVs _mockVs;
        internal HierarchyItem _item;

        private const uint MK_CONTROL = 0x0008; //winuser.h
        private const uint MK_SHIFT = 0x0004;

        public MockTreeNode(MockVs mockVs, HierarchyItem res)
        {
            _mockVs = mockVs;
            _item = res;
        }

        public void Select()
        {
            _mockVs.InvokeSync(() =>
            {
                _mockVs._uiHierarchy.ClearSelectedItems();
                _mockVs._uiHierarchy.AddSelectedItem(_item);
            });
        }

        public void AddToSelection()
        {
            _mockVs.InvokeSync(() =>
            {
                _mockVs._uiHierarchy.AddSelectedItem(_item);
            });
        }

        public void DragOntoThis(params ITreeNode[] source)
        {
            DragOntoThis(Key.None, source);
        }

        public void DragOntoThis(Key modifier, params ITreeNode[] source)
        {
            _mockVs.Invoke(() => DragOntoThisUIThread(modifier, source));
        }

        private void DragOntoThisUIThread(Key modifier, ITreeNode[] source)
        {
            var target = _item.Hierarchy as IVsHierarchyDropDataTarget;
            if (target != null)
            {
                uint effect = 0;
                uint keyState = GetKeyState(modifier);

                source[0].Select();
                for (int i = 1; i < source.Length; i++)
                {
                    source[i].AddToSelection();
                }

                MockTreeNode sourceNode = (MockTreeNode)source[0];
                var dropDataSource = (IVsHierarchyDropDataSource2)sourceNode._item.Hierarchy;
                uint okEffects;
                IDataObject data;
                IDropSource dropSource;
                ErrorHandler.ThrowOnFailure(dropDataSource.GetDropInfo(out okEffects, out data, out dropSource));

                int hr = hr = target.DragEnter(
                    data,
                    keyState,
                    _item.ItemId,
                    ref effect
                );

                if (ErrorHandler.Succeeded(hr))
                {
                    if (effect == 0)
                    {
                        return;
                    }

                    hr = target.DragOver(keyState, _item.ItemId, ref effect);

                    if (ErrorHandler.Succeeded(hr))
                    {
                        int cancel;
                        ErrorHandler.ThrowOnFailure(
                            dropDataSource.OnBeforeDropNotify(
                                data,
                                effect,
                                out cancel
                            )
                        );

                        if (cancel == 0)
                        {
                            hr = target.Drop(
                                data,
                                keyState,
                                _item.ItemId,
                                ref effect
                            );
                        }

                        int dropped = 0;
                        if (cancel == 0 && ErrorHandler.Succeeded(hr))
                        {
                            dropped = 1;
                        }
                        ErrorHandler.ThrowOnFailure(dropDataSource.OnDropNotify(dropped, effect));
                    }
                }
                return;
            }
            throw new NotImplementedException();
        }

        private uint GetKeyState(Key modifier)
        {
            switch (modifier)
            {
                case Key.LeftShift:
                    return MK_SHIFT;
                case Key.LeftCtrl:
                    return MK_CONTROL;
                case Key.None:
                    return 0;
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
