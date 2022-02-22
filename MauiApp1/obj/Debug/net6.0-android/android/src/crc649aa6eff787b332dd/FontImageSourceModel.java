package crc649aa6eff787b332dd;


public class FontImageSourceModel
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_toString:()Ljava/lang/String;:GetToStringHandler\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.BumptechGlide.FontImageSourceModel, Microsoft.Maui", FontImageSourceModel.class, __md_methods);
	}


	public FontImageSourceModel ()
	{
		super ();
		if (getClass () == FontImageSourceModel.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.BumptechGlide.FontImageSourceModel, Microsoft.Maui", "", this, new java.lang.Object[] {  });
	}


	public java.lang.String toString ()
	{
		return n_toString ();
	}

	private native java.lang.String n_toString ();

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
