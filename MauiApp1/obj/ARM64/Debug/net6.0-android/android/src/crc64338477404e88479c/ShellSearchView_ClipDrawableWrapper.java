package crc64338477404e88479c;


public class ShellSearchView_ClipDrawableWrapper
	extends androidx.appcompat.graphics.drawable.DrawableWrapper
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_draw:(Landroid/graphics/Canvas;)V:GetDraw_Landroid_graphics_Canvas_Handler\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Controls.Platform.ShellSearchView+ClipDrawableWrapper, Microsoft.Maui.Controls", ShellSearchView_ClipDrawableWrapper.class, __md_methods);
	}


	public ShellSearchView_ClipDrawableWrapper (android.graphics.drawable.Drawable p0)
	{
		super (p0);
		if (getClass () == ShellSearchView_ClipDrawableWrapper.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Platform.ShellSearchView+ClipDrawableWrapper, Microsoft.Maui.Controls", "Android.Graphics.Drawables.Drawable, Mono.Android", this, new java.lang.Object[] { p0 });
	}


	public void draw (android.graphics.Canvas p0)
	{
		n_draw (p0);
	}

	private native void n_draw (android.graphics.Canvas p0);

	private java.util.ArrayList refList;
	public void monodroidAddReference (java.lang.Object obj)
	{
		if (refList == null)
			refList = new java.util.ArrayList ();
		refList.add (obj);
	}

	public void monodroidClearReferences ()
	{
		if (refList != null)
			refList.clear ();
	}
}
