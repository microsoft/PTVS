package crc64192d9de59b079c6d;


public abstract class WebAuthenticatorCallbackActivity
	extends android.app.Activity
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onCreate:(Landroid/os/Bundle;)V:GetOnCreate_Landroid_os_Bundle_Handler\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Essentials.WebAuthenticatorCallbackActivity, Microsoft.Maui.Essentials", WebAuthenticatorCallbackActivity.class, __md_methods);
	}


	public WebAuthenticatorCallbackActivity ()
	{
		super ();
		if (getClass () == WebAuthenticatorCallbackActivity.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Essentials.WebAuthenticatorCallbackActivity, Microsoft.Maui.Essentials", "", this, new java.lang.Object[] {  });
	}


	public void onCreate (android.os.Bundle p0)
	{
		n_onCreate (p0);
	}

	private native void n_onCreate (android.os.Bundle p0);

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
