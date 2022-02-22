package mono.com.google.android.material.shape;


public class ShapeAppearancePathProvider_PathListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		com.google.android.material.shape.ShapeAppearancePathProvider.PathListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onCornerPathCreated:(Lcom/google/android/material/shape/ShapePath;Landroid/graphics/Matrix;I)V:GetOnCornerPathCreated_Lcom_google_android_material_shape_ShapePath_Landroid_graphics_Matrix_IHandler:Google.Android.Material.Shape.ShapeAppearancePathProvider/IPathListenerInvoker, Xamarin.Google.Android.Material\n" +
			"n_onEdgePathCreated:(Lcom/google/android/material/shape/ShapePath;Landroid/graphics/Matrix;I)V:GetOnEdgePathCreated_Lcom_google_android_material_shape_ShapePath_Landroid_graphics_Matrix_IHandler:Google.Android.Material.Shape.ShapeAppearancePathProvider/IPathListenerInvoker, Xamarin.Google.Android.Material\n" +
			"";
		mono.android.Runtime.register ("Google.Android.Material.Shape.ShapeAppearancePathProvider+IPathListenerImplementor, Xamarin.Google.Android.Material", ShapeAppearancePathProvider_PathListenerImplementor.class, __md_methods);
	}


	public ShapeAppearancePathProvider_PathListenerImplementor ()
	{
		super ();
		if (getClass () == ShapeAppearancePathProvider_PathListenerImplementor.class)
			mono.android.TypeManager.Activate ("Google.Android.Material.Shape.ShapeAppearancePathProvider+IPathListenerImplementor, Xamarin.Google.Android.Material", "", this, new java.lang.Object[] {  });
	}


	public void onCornerPathCreated (com.google.android.material.shape.ShapePath p0, android.graphics.Matrix p1, int p2)
	{
		n_onCornerPathCreated (p0, p1, p2);
	}

	private native void n_onCornerPathCreated (com.google.android.material.shape.ShapePath p0, android.graphics.Matrix p1, int p2);


	public void onEdgePathCreated (com.google.android.material.shape.ShapePath p0, android.graphics.Matrix p1, int p2)
	{
		n_onEdgePathCreated (p0, p1, p2);
	}

	private native void n_onEdgePathCreated (com.google.android.material.shape.ShapePath p0, android.graphics.Matrix p1, int p2);

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
