package microsoft.maui.essentials;


public class fileProvider
	extends androidx.core.content.FileProvider
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Essentials.FileProvider, Microsoft.Maui.Essentials", fileProvider.class, __md_methods);
	}


	public fileProvider ()
	{
		super ();
		if (getClass () == fileProvider.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Essentials.FileProvider, Microsoft.Maui.Essentials", "", this, new java.lang.Object[] {  });
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
