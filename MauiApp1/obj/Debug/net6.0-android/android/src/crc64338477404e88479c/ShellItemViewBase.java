package crc64338477404e88479c;


public abstract class ShellItemViewBase
	extends androidx.fragment.app.Fragment
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onDestroy:()V:GetOnDestroyHandler\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Controls.Platform.ShellItemViewBase, Microsoft.Maui.Controls", ShellItemViewBase.class, __md_methods);
	}


	public ShellItemViewBase ()
	{
		super ();
		if (getClass () == ShellItemViewBase.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Platform.ShellItemViewBase, Microsoft.Maui.Controls", "", this, new java.lang.Object[] {  });
	}


	public ShellItemViewBase (int p0)
	{
		super (p0);
		if (getClass () == ShellItemViewBase.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Platform.ShellItemViewBase, Microsoft.Maui.Controls", "System.Int32, System.Private.CoreLib", this, new java.lang.Object[] { p0 });
	}


	public void onDestroy ()
	{
		n_onDestroy ();
	}

	private native void n_onDestroy ();

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
