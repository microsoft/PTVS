package mono.androidx.core.view;


public class OnReceiveContentListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		androidx.core.view.OnReceiveContentListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onReceiveContent:(Landroid/view/View;Landroidx/core/view/ContentInfoCompat;)Landroidx/core/view/ContentInfoCompat;:GetOnReceiveContent_Landroid_view_View_Landroidx_core_view_ContentInfoCompat_Handler:AndroidX.Core.View.IOnReceiveContentListenerInvoker, Xamarin.AndroidX.Core\n" +
			"";
		mono.android.Runtime.register ("AndroidX.Core.View.IOnReceiveContentListenerImplementor, Xamarin.AndroidX.Core", OnReceiveContentListenerImplementor.class, __md_methods);
	}


	public OnReceiveContentListenerImplementor ()
	{
		super ();
		if (getClass () == OnReceiveContentListenerImplementor.class)
			mono.android.TypeManager.Activate ("AndroidX.Core.View.IOnReceiveContentListenerImplementor, Xamarin.AndroidX.Core", "", this, new java.lang.Object[] {  });
	}


	public androidx.core.view.ContentInfoCompat onReceiveContent (android.view.View p0, androidx.core.view.ContentInfoCompat p1)
	{
		return n_onReceiveContent (p0, p1);
	}

	private native androidx.core.view.ContentInfoCompat n_onReceiveContent (android.view.View p0, androidx.core.view.ContentInfoCompat p1);

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
