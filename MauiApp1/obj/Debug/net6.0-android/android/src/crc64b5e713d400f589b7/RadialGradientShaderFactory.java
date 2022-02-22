package crc64b5e713d400f589b7;


public class RadialGradientShaderFactory
	extends android.graphics.drawable.ShapeDrawable.ShaderFactory
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_resize:(II)Landroid/graphics/Shader;:GetResize_IIHandler\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Graphics.RadialGradientShaderFactory, Microsoft.Maui", RadialGradientShaderFactory.class, __md_methods);
	}


	public RadialGradientShaderFactory ()
	{
		super ();
		if (getClass () == RadialGradientShaderFactory.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Graphics.RadialGradientShaderFactory, Microsoft.Maui", "", this, new java.lang.Object[] {  });
	}


	public android.graphics.Shader resize (int p0, int p1)
	{
		return n_resize (p0, p1);
	}

	private native android.graphics.Shader n_resize (int p0, int p1);

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
