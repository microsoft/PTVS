package crc6477f0d89a9cfd64b1;


public class FormsVideoView
	extends android.widget.VideoView
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_setVideoPath:(Ljava/lang/String;)V:GetSetVideoPath_Ljava_lang_String_Handler\n" +
			"n_setVideoURI:(Landroid/net/Uri;Ljava/util/Map;)V:GetSetVideoURI_Landroid_net_Uri_Ljava_util_Map_Handler\n" +
			"n_setVideoURI:(Landroid/net/Uri;)V:GetSetVideoURI_Landroid_net_Uri_Handler\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Controls.Compatibility.Platform.Android.FormsVideoView, Microsoft.Maui.Controls.Compatibility", FormsVideoView.class, __md_methods);
	}


	public FormsVideoView (android.content.Context p0)
	{
		super (p0);
		if (getClass () == FormsVideoView.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.FormsVideoView, Microsoft.Maui.Controls.Compatibility", "Android.Content.Context, Mono.Android", this, new java.lang.Object[] { p0 });
	}


	public FormsVideoView (android.content.Context p0, android.util.AttributeSet p1)
	{
		super (p0, p1);
		if (getClass () == FormsVideoView.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.FormsVideoView, Microsoft.Maui.Controls.Compatibility", "Android.Content.Context, Mono.Android:Android.Util.IAttributeSet, Mono.Android", this, new java.lang.Object[] { p0, p1 });
	}


	public FormsVideoView (android.content.Context p0, android.util.AttributeSet p1, int p2)
	{
		super (p0, p1, p2);
		if (getClass () == FormsVideoView.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.FormsVideoView, Microsoft.Maui.Controls.Compatibility", "Android.Content.Context, Mono.Android:Android.Util.IAttributeSet, Mono.Android:System.Int32, System.Private.CoreLib", this, new java.lang.Object[] { p0, p1, p2 });
	}


	public FormsVideoView (android.content.Context p0, android.util.AttributeSet p1, int p2, int p3)
	{
		super (p0, p1, p2, p3);
		if (getClass () == FormsVideoView.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.FormsVideoView, Microsoft.Maui.Controls.Compatibility", "Android.Content.Context, Mono.Android:Android.Util.IAttributeSet, Mono.Android:System.Int32, System.Private.CoreLib:System.Int32, System.Private.CoreLib", this, new java.lang.Object[] { p0, p1, p2, p3 });
	}


	public void setVideoPath (java.lang.String p0)
	{
		n_setVideoPath (p0);
	}

	private native void n_setVideoPath (java.lang.String p0);


	public void setVideoURI (android.net.Uri p0, java.util.Map p1)
	{
		n_setVideoURI (p0, p1);
	}

	private native void n_setVideoURI (android.net.Uri p0, java.util.Map p1);


	public void setVideoURI (android.net.Uri p0)
	{
		n_setVideoURI (p0);
	}

	private native void n_setVideoURI (android.net.Uri p0);

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
