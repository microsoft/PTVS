package mono.androidx.loader.content;


public class Loader_OnLoadCanceledListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		androidx.loader.content.Loader.OnLoadCanceledListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onLoadCanceled:(Landroidx/loader/content/Loader;)V:GetOnLoadCanceled_Landroidx_loader_content_Loader_Handler:AndroidX.Loader.Content.Loader/IOnLoadCanceledListenerInvoker, Xamarin.AndroidX.Loader\n" +
			"";
		mono.android.Runtime.register ("AndroidX.Loader.Content.Loader+IOnLoadCanceledListenerImplementor, Xamarin.AndroidX.Loader", Loader_OnLoadCanceledListenerImplementor.class, __md_methods);
	}


	public Loader_OnLoadCanceledListenerImplementor ()
	{
		super ();
		if (getClass () == Loader_OnLoadCanceledListenerImplementor.class)
			mono.android.TypeManager.Activate ("AndroidX.Loader.Content.Loader+IOnLoadCanceledListenerImplementor, Xamarin.AndroidX.Loader", "", this, new java.lang.Object[] {  });
	}


	public void onLoadCanceled (androidx.loader.content.Loader p0)
	{
		n_onLoadCanceled (p0);
	}

	private native void n_onLoadCanceled (androidx.loader.content.Loader p0);

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
