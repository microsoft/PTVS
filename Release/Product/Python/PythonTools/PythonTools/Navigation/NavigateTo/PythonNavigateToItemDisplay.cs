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
