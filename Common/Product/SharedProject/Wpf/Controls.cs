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

namespace Microsoft.VisualStudioTools.Wpf
{
	public static class Controls
	{
		private static readonly Guid EnvCategory = new Guid("624ed9c3-bdfd-41fa-96c3-7c824ea32e3d");
		private static readonly Guid TreeViewCategory = new Guid("92ecf08e-8b13-4cf4-99e9-ae2692382185");

		public static readonly object BackgroundKey = VsBrushes.WindowKey;
		public static readonly object BackgroundColorKey = VsColors.WindowKey;
		public static readonly object BackgroundAccentKey = VsBrushes.ButtonFaceKey;
		public static readonly object ForegroundKey = VsBrushes.WindowTextKey;
		public static readonly object GrayTextKey = VsBrushes.GrayTextKey;
		public static readonly object HighlightKey = VsBrushes.HighlightKey;
		public static readonly object HighlightTextKey = VsBrushes.HighlightTextKey;
		public static readonly object HotTrackKey = VsBrushes.CommandBarHoverKey;
		public static readonly object HotTrackTextKey = VsBrushes.CommandBarTextHoverKey;

		public static readonly object ListBoxItemSelectedKey = TreeViewColors.SelectedItemActiveBrushKey;
		public static readonly object ListBoxItemSelectedTextKey = TreeViewColors.SelectedItemActiveTextBrushKey;

		public static readonly object TooltipBackgroundKey = new ThemeResourceKey(EnvCategory, "ToolTip", ThemeResourceKeyType.BackgroundBrush);
		public static readonly object TooltipBackgroundColorKey = new ThemeResourceKey(EnvCategory, "ToolTip", ThemeResourceKeyType.BackgroundColor);
		public static readonly object TooltipTextKey = new ThemeResourceKey(EnvCategory, "ToolTip", ThemeResourceKeyType.ForegroundBrush);
		public static readonly object TooltipTextColorKey = new ThemeResourceKey(EnvCategory, "ToolTip", ThemeResourceKeyType.ForegroundColor);

		public static readonly object InfoBackgroundKey = VsBrushes.InfoBackgroundKey;
		public static readonly object InfoBackgroundColorKey = VsColors.InfoBackgroundKey;
		public static readonly object InfoTextKey = VsBrushes.InfoTextKey;
		public static readonly object InfoTextColorKey = VsColors.InfoTextKey;

		public static readonly object HyperlinkKey = VsBrushes.ControlLinkTextKey;
		public static readonly object HyperlinkHoverKey = VsBrushes.ControlLinkTextHoverKey;

		public static readonly object ControlBackgroundKey = VsBrushes.ComboBoxBackgroundKey;
		public static readonly object ControlForegroundKey = VsBrushes.WindowTextKey;
		public static readonly object ControlBorderKey = VsBrushes.ComboBoxBorderKey;
		public static readonly object ControlBackgroundHoverKey = VsBrushes.ComboBoxMouseOverBackgroundGradientKey;
		public static readonly object ControlBorderHoverKey = VsBrushes.ComboBoxMouseOverBorderKey;
		public static readonly object ControlForegroundHoverKey = VsBrushes.CommandBarTextHoverKey;
		public static readonly object ControlBackgroundPressedKey = VsBrushes.ComboBoxMouseDownBackgroundKey;
		public static readonly object ControlForegroundPressedKey = VsBrushes.ComboBoxGlyphKey;
		public static readonly object ControlBorderPressedKey = VsBrushes.ComboBoxMouseDownBorderKey;
		public static readonly object ControlBackgroundSelectedKey = VsBrushes.ComboBoxMouseDownBackgroundKey;
		public static readonly object ControlForegroundSelectedKey = VsBrushes.ComboBoxGlyphKey;
		public static readonly object ControlBorderSelectedKey = VsBrushes.ComboBoxMouseOverBorderKey;
		public static readonly object ControlBackgroundDisabledKey = VsBrushes.ComboBoxDisabledBackgroundKey;
		public static readonly object ControlForegroundDisabledKey = VsBrushes.ComboBoxDisabledGlyphKey;
		public static readonly object ControlBorderDisabledKey = VsBrushes.ComboBoxDisabledBorderKey;

		public static readonly object ControlBackgroundColorKey = VsColors.ComboBoxBackgroundKey;
		public static readonly object ControlForegroundColorKey = VsColors.WindowTextKey;
		public static readonly object ControlBorderColorKey = VsColors.ComboBoxBorderKey;
		public static readonly object ControlBackgroundHoverColorKey = VsColors.ComboBoxMouseOverBackgroundMiddle1Key;
		public static readonly object ControlBorderHoverColorKey = VsColors.ComboBoxMouseOverGlyphKey;
		public static readonly object ControlBackgroundPressedColorKey = VsColors.ComboBoxMouseDownBackgroundKey;
		public static readonly object ControlForegroundPressedColorKey = VsColors.ComboBoxGlyphKey;
		public static readonly object ControlBorderPressedColorKey = VsColors.ComboBoxMouseDownBorderKey;
		public static readonly object ControlBackgroundSelectedColorKey = VsColors.ComboBoxMouseDownBackgroundKey;
		public static readonly object ControlForegroundSelectedColorKey = VsColors.ComboBoxGlyphKey;
		public static readonly object ControlBorderSelectedColorKey = VsColors.ComboBoxMouseOverBorderKey;
		public static readonly object ControlBackgroundDisabledColorKey = VsColors.ComboBoxDisabledBackgroundKey;
		public static readonly object ControlForegroundDisabledColorKey = VsColors.ComboBoxDisabledGlyphKey;
		public static readonly object ControlBorderDisabledColorKey = VsColors.ComboBoxDisabledBorderKey;

		public static readonly object ComboBoxPopupBackgroundKey = VsBrushes.ComboBoxPopupBackgroundGradientKey;
		public static readonly object ComboBoxPopupBorderKey = VsBrushes.ComboBoxPopupBorderKey;
		public static readonly object ComboBoxPopupForegroundKey = VsBrushes.WindowTextKey;

		public static readonly object ButtonForegroundPressedKey = VsBrushes.ToolWindowButtonDownActiveGlyphKey;
		public static readonly object ButtonBackgroundPressedKey = VsBrushes.ToolWindowButtonDownKey;
		public static readonly object ButtonForegroundHoverKey = VsBrushes.ToolWindowButtonHoverActiveGlyphKey;
		public static readonly object ButtonBackgroundHoverKey = VsBrushes.ToolWindowButtonHoverActiveKey;
		public static readonly object ButtonBorderHoverKey = VsBrushes.ToolWindowButtonHoverActiveBorderKey;
		public static readonly object ButtonForegroundPressedColorKey = VsColors.ToolWindowButtonDownActiveGlyphKey;
		public static readonly object ButtonBackgroundPressedColorKey = VsColors.ToolWindowButtonDownKey;
		public static readonly object ButtonForegroundHoverColorKey = VsColors.ComboBoxMouseOverGlyphKey;
		public static readonly object ButtonBackgroundHoverColorKey = VsColors.ComboBoxMouseOverBackgroundBeginKey;
		public static readonly object ButtonBorderHoverColorKey = VsColors.ToolWindowButtonHoverActiveBorderKey;

		public static readonly object ScrollBarBackgroundKey = VsBrushes.ScrollBarBackgroundKey;
		public static readonly object ScrollBarThumbBackgroundKey = VsBrushes.ScrollBarThumbBackgroundKey;
		public static readonly object ScrollBarThumbBackgroundHoverKey = VsBrushes.ScrollBarThumbMouseOverBackgroundKey;
		public static readonly object ScrollBarThumbBackgroundPressedKey = VsBrushes.ScrollBarThumbPressedBackgroundKey;
		public static readonly object ScrollBarArrowKey = VsBrushes.ScrollBarThumbGlyphKey;
		public static readonly object ScrollBarArrowHoverKey = VsBrushes.GrayTextKey;
		public static readonly object ScrollBarArrowPressedKey = VsBrushes.WindowTextKey;
		public static readonly object ScrollBarArrowDisabledKey = VsBrushes.ScrollBarThumbGlyphKey;
		public static readonly object ScrollBarArrowBackgroundKey = VsBrushes.ScrollBarArrowBackgroundKey;
		public static readonly object ScrollBarArrowBackgroundHoverKey = VsBrushes.ScrollBarArrowMouseOverBackgroundKey;
		public static readonly object ScrollBarArrowBackgroundPressedKey = VsBrushes.ScrollBarArrowPressedBackgroundKey;
		public static readonly object ScrollBarArrowBackgroundDisabledKey = VsBrushes.ScrollBarArrowDisabledBackgroundKey;

		public static readonly object TreeViewBackgroundKey = new ThemeResourceKey(TreeViewCategory, "Background", ThemeResourceKeyType.BackgroundBrush);
		public static readonly object TreeViewBackgroundColorKey = new ThemeResourceKey(TreeViewCategory, "Background", ThemeResourceKeyType.BackgroundColor);
		public static readonly object TreeViewForegroundKey = new ThemeResourceKey(TreeViewCategory, "Background", ThemeResourceKeyType.ForegroundBrush);

		public static readonly object TreeViewItemSelectedBackgroundKey = new ThemeResourceKey(TreeViewCategory, "SelectedItemActive", ThemeResourceKeyType.BackgroundBrush);
		public static readonly object TreeViewItemSelectedBackgroundColorKey = new ThemeResourceKey(TreeViewCategory, "SelectedItemActive", ThemeResourceKeyType.BackgroundColor);
		public static readonly object TreeViewItemSelectedForegroundKey = new ThemeResourceKey(TreeViewCategory, "SelectedItemActive", ThemeResourceKeyType.ForegroundBrush);
		public static readonly object TreeViewItemInactiveSelectedBackgroundKey = new ThemeResourceKey(TreeViewCategory, "SelectedItemInactive", ThemeResourceKeyType.BackgroundBrush);
		public static readonly object TreeViewItemInactiveSelectedBackgroundColorKey = new ThemeResourceKey(TreeViewCategory, "SelectedItemInactive", ThemeResourceKeyType.BackgroundColor);
		public static readonly object TreeViewItemInactiveSelectedForegroundKey = new ThemeResourceKey(TreeViewCategory, "SelectedItemInactive", ThemeResourceKeyType.ForegroundBrush);


#if DEV11_OR_LATER
        public static readonly object SearchGlyphBrushKey = SearchControlColors.SearchGlyphBrushKey;
#else
		public static readonly object SearchGlyphBrushKey = VsBrushes.WindowTextKey;
#endif

		public static readonly BitmapSource UacShield = CreateUacShield();

		private static BitmapSource CreateUacShield()
		{
			if (Environment.OSVersion.Version.Major >= 6)
			{
				var sii = new NativeMethods.SHSTOCKICONINFO();
				sii.cbSize = (UInt32)Marshal.SizeOf(typeof(NativeMethods.SHSTOCKICONINFO));

				Marshal.ThrowExceptionForHR(NativeMethods.SHGetStockIconInfo(77, 0x0101, ref sii));
				try
				{
					return Imaging.CreateBitmapSourceFromHIcon(
						sii.hIcon,
						Int32Rect.Empty,
						BitmapSizeOptions.FromEmptyOptions());
				}
				finally
				{
					NativeMethods.DestroyIcon(sii.hIcon);
				}
			}
			else
			{
				return Imaging.CreateBitmapSourceFromHIcon(
					SystemIcons.Shield.Handle,
					Int32Rect.Empty,
					BitmapSizeOptions.FromEmptyOptions());
			}
		}
	}
}
