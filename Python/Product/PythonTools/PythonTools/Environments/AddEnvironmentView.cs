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

namespace Microsoft.PythonTools.Environments
{
    sealed class AddEnvironmentView : DependencyObject, IDisposable
    {
        public AddEnvironmentView(IEnumerable<EnvironmentViewBase> pages, EnvironmentViewBase selected)
        {
            if (pages == null)
            {
                throw new ArgumentNullException(nameof(pages));
            }

            Pages = new ObservableCollection<EnvironmentViewBase>(pages);
            PagesView = new ListCollectionView(Pages);
            PagesView.MoveCurrentTo(selected);
        }

        public ObservableCollection<EnvironmentViewBase> Pages { get; }

        public ListCollectionView PagesView { get; }

        public void Dispose()
        {
            foreach (var view in Pages)
            {
                view.Dispose();
            }
        }
    }
}
