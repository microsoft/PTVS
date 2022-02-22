package crc64338477404e88479c;


public class GradientStrokeDrawable
	extends android.graphics.drawable.PaintDrawable
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onDraw:(Landroid/graphics/drawable/shapes/Shape;Landroid/graphics/Canvas;Landroid/graphics/Paint;)V:GetOnDraw_Landroid_graphics_drawable_shapes_Shape_Landroid_graphics_Canvas_Landroid_graphics_Paint_Handler\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Controls.Platform.GradientStrokeDrawable, Microsoft.Maui.Controls", GradientStrokeDrawable.class, __md_methods);
	}


	public GradientStrokeDrawable ()
	{
		super ();
		if (getClass () == GradientStrokeDrawable.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Platform.GradientStrokeDrawable, Microsoft.Maui.Controls", "", this, new java.lang.Object[] {  });
	}


	public GradientStrokeDrawable (int p0)
	{
		super (p0);
		if (getClass () == GradientStrokeDrawable.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Platform.GradientStrokeDrawable, Microsoft.Maui.Controls", "Android.Graphics.Color, Mono.Android", this, new java.lang.Object[] { p0 });
	}


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
