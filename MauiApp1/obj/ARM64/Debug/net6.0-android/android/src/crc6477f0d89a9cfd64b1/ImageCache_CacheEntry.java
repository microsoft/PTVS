package crc6477f0d89a9cfd64b1;


public class ImageCache_CacheEntry
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Controls.Compatibility.Platform.Android.ImageCache+CacheEntry, Microsoft.Maui.Controls.Compatibility", ImageCache_CacheEntry.class, __md_methods);
	}


	public ImageCache_CacheEntry ()
	{
		super ();
		if (getClass () == ImageCache_CacheEntry.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.ImageCache+CacheEntry, Microsoft.Maui.Controls.Compatibility", "", this, new java.lang.Object[] {  });
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
