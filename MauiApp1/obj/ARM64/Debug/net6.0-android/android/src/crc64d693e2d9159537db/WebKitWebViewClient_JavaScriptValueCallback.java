package crc64d693e2d9159537db;


public class WebKitWebViewClient_JavaScriptValueCallback
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		android.webkit.ValueCallback
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onReceiveValue:(Ljava/lang/Object;)V:GetOnReceiveValue_Ljava_lang_Object_Handler:Android.Webkit.IValueCallbackInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"";
		mono.android.Runtime.register ("Microsoft.AspNetCore.Components.WebView.Maui.WebKitWebViewClient+JavaScriptValueCallback, Microsoft.AspNetCore.Components.WebView.Maui", WebKitWebViewClient_JavaScriptValueCallback.class, __md_methods);
	}


	public WebKitWebViewClient_JavaScriptValueCallback ()
	{
		super ();
		if (getClass () == WebKitWebViewClient_JavaScriptValueCallback.class)
			mono.android.TypeManager.Activate ("Microsoft.AspNetCore.Components.WebView.Maui.WebKitWebViewClient+JavaScriptValueCallback, Microsoft.AspNetCore.Components.WebView.Maui", "", this, new java.lang.Object[] {  });
	}


	public void onReceiveValue (java.lang.Object p0)
	{
		n_onReceiveValue (p0);
	}

	private native void n_onReceiveValue (java.lang.Object p0);

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
