package crc6477f0d89a9cfd64b1;


public class ImageCache_FormsLruCache
	extends android.util.LruCache
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_sizeOf:(Ljava/lang/Object;Ljava/lang/Object;)I:GetSizeOf_Ljava_lang_Object_Ljava_lang_Object_Handler\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Controls.Compatibility.Platform.Android.ImageCache+FormsLruCache, Microsoft.Maui.Controls.Compatibility", ImageCache_FormsLruCache.class, __md_methods);
	}


	public ImageCache_FormsLruCache (int p0)
	{
		super (p0);
		if (getClass () == ImageCache_FormsLruCache.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.ImageCache+FormsLruCache, Microsoft.Maui.Controls.Compatibility", "System.Int32, System.Private.CoreLib", this, new java.lang.Object[] { p0 });
	}


	public int sizeOf (java.lang.Object p0, java.lang.Object p1)
	{
		return n_sizeOf (p0, p1);
	}

	private native int n_sizeOf (java.lang.Object p0, java.lang.Object p1);

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
