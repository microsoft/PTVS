package crc643f2b18b2570eaa5a;


public class PlatformGraphicsView
	extends android.view.View
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_draw:(Landroid/graphics/Canvas;)V:GetDraw_Landroid_graphics_Canvas_Handler\n" +
			"n_onSizeChanged:(IIII)V:GetOnSizeChanged_IIIIHandler\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Graphics.Platform.PlatformGraphicsView, Microsoft.Maui.Graphics", PlatformGraphicsView.class, __md_methods);
	}


	public PlatformGraphicsView (android.content.Context p0)
	{
		super (p0);
		if (getClass () == PlatformGraphicsView.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Graphics.Platform.PlatformGraphicsView, Microsoft.Maui.Graphics", "Android.Content.Context, Mono.Android", this, new java.lang.Object[] { p0 });
	}


	public PlatformGraphicsView (android.content.Context p0, android.util.AttributeSet p1)
	{
		super (p0, p1);
		if (getClass () == PlatformGraphicsView.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Graphics.Platform.PlatformGraphicsView, Microsoft.Maui.Graphics", "Android.Content.Context, Mono.Android:Android.Util.IAttributeSet, Mono.Android", this, new java.lang.Object[] { p0, p1 });
	}


	public PlatformGraphicsView (android.content.Context p0, android.util.AttributeSet p1, int p2)
	{
		super (p0, p1, p2);
		if (getClass () == PlatformGraphicsView.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Graphics.Platform.PlatformGraphicsView, Microsoft.Maui.Graphics", "Android.Content.Context, Mono.Android:Android.Util.IAttributeSet, Mono.Android:System.Int32, System.Private.CoreLib", this, new java.lang.Object[] { p0, p1, p2 });
	}


	public PlatformGraphicsView (android.content.Context p0, android.util.AttributeSet p1, int p2, int p3)
	{
		super (p0, p1, p2, p3);
		if (getClass () == PlatformGraphicsView.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Graphics.Platform.PlatformGraphicsView, Microsoft.Maui.Graphics", "Android.Content.Context, Mono.Android:Android.Util.IAttributeSet, Mono.Android:System.Int32, System.Private.CoreLib:System.Int32, System.Private.CoreLib", this, new java.lang.Object[] { p0, p1, p2, p3 });
	}


	public void draw (android.graphics.Canvas p0)
	{
		n_draw (p0);
	}

	private native void n_draw (android.graphics.Canvas p0);


	public void onSizeChanged (int p0, int p1, int p2, int p3)
	{
		n_onSizeChanged (p0, p1, p2, p3);
	}

	private native void n_onSizeChanged (int p0, int p1, int p2, int p3);

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
