package crc6477f0d89a9cfd64b1;


public abstract class ShellItemRendererBase
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
		mono.android.Runtime.register ("Microsoft.Maui.Controls.Compatibility.Platform.Android.ShellItemRendererBase, Microsoft.Maui.Controls.Compatibility", ShellItemRendererBase.class, __md_methods);
	}


	public ShellItemRendererBase ()
	{
		super ();
		if (getClass () == ShellItemRendererBase.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.ShellItemRendererBase, Microsoft.Maui.Controls.Compatibility", "", this, new java.lang.Object[] {  });
	}


	public ShellItemRendererBase (int p0)
	{
		super (p0);
		if (getClass () == ShellItemRendererBase.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.ShellItemRendererBase, Microsoft.Maui.Controls.Compatibility", "System.Int32, System.Private.CoreLib", this, new java.lang.Object[] { p0 });
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
