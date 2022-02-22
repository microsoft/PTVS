package crc645d80431ce5f73f11;


public abstract class EdgeSnapHelper
	extends crc645d80431ce5f73f11.NongreedySnapHelper
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Controls.Handlers.Items.EdgeSnapHelper, Microsoft.Maui.Controls", EdgeSnapHelper.class, __md_methods);
	}


	public EdgeSnapHelper ()
	{
		super ();
		if (getClass () == EdgeSnapHelper.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Handlers.Items.EdgeSnapHelper, Microsoft.Maui.Controls", "", this, new java.lang.Object[] {  });
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
