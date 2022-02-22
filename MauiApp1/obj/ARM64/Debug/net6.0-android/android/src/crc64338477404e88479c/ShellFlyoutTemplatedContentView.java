package crc64338477404e88479c;


public class ShellFlyoutTemplatedContentView
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		com.google.android.material.appbar.AppBarLayout.OnOffsetChangedListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onOffsetChanged:(Lcom/google/android/material/appbar/AppBarLayout;I)V:GetOnOffsetChanged_Lcom_google_android_material_appbar_AppBarLayout_IHandler:Google.Android.Material.AppBar.AppBarLayout/IOnOffsetChangedListenerInvoker, Xamarin.Google.Android.Material\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Controls.Platform.ShellFlyoutTemplatedContentView, Microsoft.Maui.Controls", ShellFlyoutTemplatedContentView.class, __md_methods);
	}


	public ShellFlyoutTemplatedContentView ()
	{
		super ();
		if (getClass () == ShellFlyoutTemplatedContentView.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Platform.ShellFlyoutTemplatedContentView, Microsoft.Maui.Controls", "", this, new java.lang.Object[] {  });
	}


	public void onOffsetChanged (com.google.android.material.appbar.AppBarLayout p0, int p1)
	{
		n_onOffsetChanged (p0, p1);
	}

	private native void n_onOffsetChanged (com.google.android.material.appbar.AppBarLayout p0, int p1);

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
