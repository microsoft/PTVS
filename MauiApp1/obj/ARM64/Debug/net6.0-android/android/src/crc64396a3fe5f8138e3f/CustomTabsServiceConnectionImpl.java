package crc64396a3fe5f8138e3f;


public class CustomTabsServiceConnectionImpl
	extends androidx.browser.customtabs.CustomTabsServiceConnection
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onCustomTabsServiceConnected:(Landroid/content/ComponentName;Landroidx/browser/customtabs/CustomTabsClient;)V:GetOnCustomTabsServiceConnected_Landroid_content_ComponentName_Landroidx_browser_customtabs_CustomTabsClient_Handler\n" +
			"n_onServiceDisconnected:(Landroid/content/ComponentName;)V:GetOnServiceDisconnected_Landroid_content_ComponentName_Handler\n" +
			"";
		mono.android.Runtime.register ("AndroidX.Browser.CustomTabs.CustomTabsServiceConnectionImpl, Xamarin.AndroidX.Browser", CustomTabsServiceConnectionImpl.class, __md_methods);
	}


	public CustomTabsServiceConnectionImpl ()
	{
		super ();
		if (getClass () == CustomTabsServiceConnectionImpl.class)
			mono.android.TypeManager.Activate ("AndroidX.Browser.CustomTabs.CustomTabsServiceConnectionImpl, Xamarin.AndroidX.Browser", "", this, new java.lang.Object[] {  });
	}


	public void onCustomTabsServiceConnected (android.content.ComponentName p0, androidx.browser.customtabs.CustomTabsClient p1)
	{
		n_onCustomTabsServiceConnected (p0, p1);
	}

	private native void n_onCustomTabsServiceConnected (android.content.ComponentName p0, androidx.browser.customtabs.CustomTabsClient p1);


	public void onServiceDisconnected (android.content.ComponentName p0)
	{
		n_onServiceDisconnected (p0);
	}

	private native void n_onServiceDisconnected (android.content.ComponentName p0);

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
