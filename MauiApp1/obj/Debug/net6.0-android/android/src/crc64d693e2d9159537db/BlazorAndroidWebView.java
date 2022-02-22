package crc64d693e2d9159537db;


public class BlazorAndroidWebView
	extends android.webkit.WebView
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onKeyDown:(ILandroid/view/KeyEvent;)Z:GetOnKeyDown_ILandroid_view_KeyEvent_Handler\n" +
			"";
		mono.android.Runtime.register ("Microsoft.AspNetCore.Components.WebView.Maui.BlazorAndroidWebView, Microsoft.AspNetCore.Components.WebView.Maui", BlazorAndroidWebView.class, __md_methods);
	}


	public BlazorAndroidWebView (android.content.Context p0)
	{
		super (p0);
		if (getClass () == BlazorAndroidWebView.class)
			mono.android.TypeManager.Activate ("Microsoft.AspNetCore.Components.WebView.Maui.BlazorAndroidWebView, Microsoft.AspNetCore.Components.WebView.Maui", "Android.Content.Context, Mono.Android", this, new java.lang.Object[] { p0 });
	}


	public BlazorAndroidWebView (android.content.Context p0, android.util.AttributeSet p1)
	{
		super (p0, p1);
		if (getClass () == BlazorAndroidWebView.class)
			mono.android.TypeManager.Activate ("Microsoft.AspNetCore.Components.WebView.Maui.BlazorAndroidWebView, Microsoft.AspNetCore.Components.WebView.Maui", "Android.Content.Context, Mono.Android:Android.Util.IAttributeSet, Mono.Android", this, new java.lang.Object[] { p0, p1 });
	}


	public BlazorAndroidWebView (android.content.Context p0, android.util.AttributeSet p1, int p2)
	{
		super (p0, p1, p2);
		if (getClass () == BlazorAndroidWebView.class)
			mono.android.TypeManager.Activate ("Microsoft.AspNetCore.Components.WebView.Maui.BlazorAndroidWebView, Microsoft.AspNetCore.Components.WebView.Maui", "Android.Content.Context, Mono.Android:Android.Util.IAttributeSet, Mono.Android:System.Int32, System.Private.CoreLib", this, new java.lang.Object[] { p0, p1, p2 });
	}


	public BlazorAndroidWebView (android.content.Context p0, android.util.AttributeSet p1, int p2, boolean p3)
	{
		super (p0, p1, p2, p3);
		if (getClass () == BlazorAndroidWebView.class)
			mono.android.TypeManager.Activate ("Microsoft.AspNetCore.Components.WebView.Maui.BlazorAndroidWebView, Microsoft.AspNetCore.Components.WebView.Maui", "Android.Content.Context, Mono.Android:Android.Util.IAttributeSet, Mono.Android:System.Int32, System.Private.CoreLib:System.Boolean, System.Private.CoreLib", this, new java.lang.Object[] { p0, p1, p2, p3 });
	}


	public BlazorAndroidWebView (android.content.Context p0, android.util.AttributeSet p1, int p2, int p3)
	{
		super (p0, p1, p2, p3);
		if (getClass () == BlazorAndroidWebView.class)
			mono.android.TypeManager.Activate ("Microsoft.AspNetCore.Components.WebView.Maui.BlazorAndroidWebView, Microsoft.AspNetCore.Components.WebView.Maui", "Android.Content.Context, Mono.Android:Android.Util.IAttributeSet, Mono.Android:System.Int32, System.Private.CoreLib:System.Int32, System.Private.CoreLib", this, new java.lang.Object[] { p0, p1, p2, p3 });
	}


	public boolean onKeyDown (int p0, android.view.KeyEvent p1)
	{
		return n_onKeyDown (p0, p1);
	}

	private native boolean n_onKeyDown (int p0, android.view.KeyEvent p1);

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
