package crc6477f0d89a9cfd64b1;


public abstract class EdgeSnapHelper
	extends crc6477f0d89a9cfd64b1.NongreedySnapHelper
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Controls.Compatibility.Platform.Android.EdgeSnapHelper, Microsoft.Maui.Controls.Compatibility", EdgeSnapHelper.class, __md_methods);
	}


	public EdgeSnapHelper ()
	{
		super ();
		if (getClass () == EdgeSnapHelper.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.EdgeSnapHelper, Microsoft.Maui.Controls.Compatibility", "", this, new java.lang.Object[] {  });
	}

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
