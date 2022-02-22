package mono.androidx.loader.content;


public class Loader_OnLoadCompleteListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		androidx.loader.content.Loader.OnLoadCompleteListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onLoadComplete:(Landroidx/loader/content/Loader;Ljava/lang/Object;)V:GetOnLoadComplete_Landroidx_loader_content_Loader_Ljava_lang_Object_Handler:AndroidX.Loader.Content.Loader/IOnLoadCompleteListenerInvoker, Xamarin.AndroidX.Loader\n" +
			"";
		mono.android.Runtime.register ("AndroidX.Loader.Content.Loader+IOnLoadCompleteListenerImplementor, Xamarin.AndroidX.Loader", Loader_OnLoadCompleteListenerImplementor.class, __md_methods);
	}


	public Loader_OnLoadCompleteListenerImplementor ()
	{
		super ();
		if (getClass () == Loader_OnLoadCompleteListenerImplementor.class)
			mono.android.TypeManager.Activate ("AndroidX.Loader.Content.Loader+IOnLoadCompleteListenerImplementor, Xamarin.AndroidX.Loader", "", this, new java.lang.Object[] {  });
	}


	public void onLoadComplete (androidx.loader.content.Loader p0, java.lang.Object p1)
	{
		n_onLoadComplete (p0, p1);
	}

	private native void n_onLoadComplete (androidx.loader.content.Loader p0, java.lang.Object p1);

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
