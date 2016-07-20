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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.NavigateTo.Interfaces;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Navigation;

namespace Microsoft.PythonTools.Navigation.NavigateTo {
    internal class PythonNavigateToItemDisplay : INavigateToItemDisplay {
        private static readonly Dictionary<StandardGlyphGroup, Icon> _iconCache = new Dictionary<StandardGlyphGroup, Icon>();
        private readonly NavigateToItem _item;
        private readonly LibraryNode _node;
        private readonly Icon _icon;
        private readonly ReadOnlyCollection<DescriptionItem> _descrItems;

        public PythonNavigateToItemDisplay(NavigateToItem item) {
            _item = item;
            var tag = (PythonNavigateToItemProvider.ItemTag)item.Tag;
            _node = tag.Node;
            _icon = GetIcon(tag.GlyphService, _node.GlyphType);

            var descrItems = new List<DescriptionItem>();

            IVsHierarchy hier;
            uint itemId;
            uint itemsCount;
            _node.SourceItems(out hier, out itemId, out itemsCount);
            if (hier != null) {
                descrItems.Add(new DescriptionItem(
                    Array.AsReadOnly(new[] { new DescriptionRun("Project:", bold: true) }),
                    Array.AsReadOnly(new[] { new DescriptionRun(hier.GetProject().FullName) })));

                string fileName;
                hier.GetCanonicalName(itemId, out fileName);
                if (fileName != null) {
                    descrItems.Add(new DescriptionItem(
                        Array.AsReadOnly(new[] { new DescriptionRun("File:", bold: true) }),
                        Array.AsReadOnly(new[] { new DescriptionRun(fileName) })));

                    var commonNode = _node as CommonLibraryNode;
                    if (commonNode != null && commonNode.CanGoToSource) {
                        descrItems.Add(new DescriptionItem(
                            Array.AsReadOnly(new[] { new DescriptionRun("Line:", bold: true) }),
                            Array.AsReadOnly(new[] { new DescriptionRun((commonNode.SourceSpan.iStartLine + 1).ToString()) })));
                    }
                }
            }

            _descrItems = descrItems.AsReadOnly();
        }

        public string Name {
            get { return _item.Name; }
        }

        public string AdditionalInformation {
            get { return ""; }
        }

        public string Description {
            get { return ""; }
        }

        public ReadOnlyCollection<DescriptionItem> DescriptionItems {
            get { return _descrItems; }
        }

        public Icon Glyph {
            get {
                return _icon;
            }
        }

        public void NavigateTo() {
            _node.GotoSource(VSOBJGOTOSRCTYPE.GS_DEFINITION);
        }

        private static Icon GetIcon(IGlyphService glyphService, StandardGlyphGroup glyphGroup) {
            Icon icon = null;
            if (_iconCache.TryGetValue(glyphGroup, out icon)) {
                return icon;
            }

            BitmapSource glyph = glyphService.GetGlyph(glyphGroup, StandardGlyphItem.GlyphItemPublic) as BitmapSource;
            if (glyph != null) {
                Bitmap bmp = new Bitmap(glyph.PixelWidth, glyph.PixelHeight, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                glyph.CopyPixels(Int32Rect.Empty, bmpData.Scan0, bmpData.Height * bmpData.Stride, bmpData.Stride);
                bmp.UnlockBits(bmpData);
                icon = Icon.FromHandle(bmp.GetHicon());
            }

            _iconCache[glyphGroup] = icon;
            return icon;
        }
    }
}
