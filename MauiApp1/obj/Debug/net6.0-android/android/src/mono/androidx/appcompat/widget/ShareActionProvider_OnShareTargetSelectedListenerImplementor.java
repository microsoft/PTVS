package mono.androidx.appcompat.widget;


public class ShareActionProvider_OnShareTargetSelectedListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		androidx.appcompat.widget.ShareActionProvider.OnShareTargetSelectedListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onShareTargetSelected:(Landroidx/appcompat/widget/ShareActionProvider;Landroid/content/Intent;)Z:GetOnShareTargetSelected_Landroidx_appcompat_widget_ShareActionProvider_Landroid_content_Intent_Handler:AndroidX.AppCompat.Widget.ShareActionProvider/IOnShareTargetSelectedListenerInvoker, Xamarin.AndroidX.AppCompat\n" +
			"";
		mono.android.Runtime.register ("AndroidX.AppCompat.Widget.ShareActionProvider+IOnShareTargetSelectedListenerImplementor, Xamarin.AndroidX.AppCompat", ShareActionProvider_OnShareTargetSelectedListenerImplementor.class, __md_methods);
	}


	public ShareActionProvider_OnShareTargetSelectedListenerImplementor ()
	{
		super ();
		if (getClass () == ShareActionProvider_OnShareTargetSelectedListenerImplementor.class)
			mono.android.TypeManager.Activate ("AndroidX.AppCompat.Widget.ShareActionProvider+IOnShareTargetSelectedListenerImplementor, Xamarin.AndroidX.AppCompat", "", this, new java.lang.Object[] {  });
	}


	public boolean onShareTargetSelected (androidx.appcompat.widget.ShareActionProvider p0, android.content.Intent p1)
	{
		return n_onShareTargetSelected (p0, p1);
	}

	private native boolean n_onShareTargetSelected (androidx.appcompat.widget.ShareActionProvider p0, android.content.Intent p1);

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
