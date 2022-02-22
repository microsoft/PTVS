package crc64b5e713d400f589b7;


public class MauiDrawable
	extends android.graphics.drawable.PaintDrawable
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onBoundsChange:(Landroid/graphics/Rect;)V:GetOnBoundsChange_Landroid_graphics_Rect_Handler\n" +
			"n_onDraw:(Landroid/graphics/drawable/shapes/Shape;Landroid/graphics/Canvas;Landroid/graphics/Paint;)V:GetOnDraw_Landroid_graphics_drawable_shapes_Shape_Landroid_graphics_Canvas_Landroid_graphics_Paint_Handler\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Graphics.MauiDrawable, Microsoft.Maui", MauiDrawable.class, __md_methods);
	}


	public MauiDrawable ()
	{
		super ();
		if (getClass () == MauiDrawable.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Graphics.MauiDrawable, Microsoft.Maui", "", this, new java.lang.Object[] {  });
	}


	public MauiDrawable (int p0)
	{
		super (p0);
		if (getClass () == MauiDrawable.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Graphics.MauiDrawable, Microsoft.Maui", "Android.Graphics.Color, Mono.Android", this, new java.lang.Object[] { p0 });
	}

	public MauiDrawable (android.content.Context p0)
	{
		super ();
		if (getClass () == MauiDrawable.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Graphics.MauiDrawable, Microsoft.Maui", "Android.Content.Context, Mono.Android", this, new java.lang.Object[] { p0 });
	}


	public void onBoundsChange (android.graphics.Rect p0)
	{
		n_onBoundsChange (p0);
	}

	private native void n_onBoundsChange (android.graphics.Rect p0);


	public void onDraw (android.graphics.drawable.shapes.Shape p0, android.graphics.Canvas p1, android.graphics.Paint p2)
	{
		n_onDraw (p0, p1, p2);
	}

	private native void n_onDraw (android.graphics.drawable.shapes.Shape p0, android.graphics.Canvas p1, android.graphics.Paint p2);

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
