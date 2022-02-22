package crc64d693e2d9159537db;


public class AndroidWebKitWebViewManager_BlazorWebMessageCallback
	extends android.webkit.WebMessagePort.WebMessageCallback
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onMessage:(Landroid/webkit/WebMessagePort;Landroid/webkit/WebMessage;)V:GetOnMessage_Landroid_webkit_WebMessagePort_Landroid_webkit_WebMessage_Handler\n" +
			"";
		mono.android.Runtime.register ("Microsoft.AspNetCore.Components.WebView.Maui.AndroidWebKitWebViewManager+BlazorWebMessageCallback, Microsoft.AspNetCore.Components.WebView.Maui", AndroidWebKitWebViewManager_BlazorWebMessageCallback.class, __md_methods);
	}


	public AndroidWebKitWebViewManager_BlazorWebMessageCallback ()
	{
		super ();
		if (getClass () == AndroidWebKitWebViewManager_BlazorWebMessageCallback.class)
			mono.android.TypeManager.Activate ("Microsoft.AspNetCore.Components.WebView.Maui.AndroidWebKitWebViewManager+BlazorWebMessageCallback, Microsoft.AspNetCore.Components.WebView.Maui", "", this, new java.lang.Object[] {  });
	}


	public void onMessage (android.webkit.WebMessagePort p0, android.webkit.WebMessage p1)
	{
		n_onMessage (p0, p1);
	}

	private native void n_onMessage (android.webkit.WebMessagePort p0, android.webkit.WebMessage p1);

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
