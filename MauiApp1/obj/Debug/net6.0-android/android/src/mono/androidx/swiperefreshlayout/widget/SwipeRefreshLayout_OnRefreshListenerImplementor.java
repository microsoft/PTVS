package mono.androidx.swiperefreshlayout.widget;


public class SwipeRefreshLayout_OnRefreshListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		androidx.swiperefreshlayout.widget.SwipeRefreshLayout.OnRefreshListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onRefresh:()V:GetOnRefreshHandler:AndroidX.SwipeRefreshLayout.Widget.SwipeRefreshLayout/IOnRefreshListenerInvoker, Xamarin.AndroidX.SwipeRefreshLayout\n" +
			"";
		mono.android.Runtime.register ("AndroidX.SwipeRefreshLayout.Widget.SwipeRefreshLayout+IOnRefreshListenerImplementor, Xamarin.AndroidX.SwipeRefreshLayout", SwipeRefreshLayout_OnRefreshListenerImplementor.class, __md_methods);
	}


	public SwipeRefreshLayout_OnRefreshListenerImplementor ()
	{
		super ();
		if (getClass () == SwipeRefreshLayout_OnRefreshListenerImplementor.class)
			mono.android.TypeManager.Activate ("AndroidX.SwipeRefreshLayout.Widget.SwipeRefreshLayout+IOnRefreshListenerImplementor, Xamarin.AndroidX.SwipeRefreshLayout", "", this, new java.lang.Object[] {  });
	}


	public void onRefresh ()
	{
		n_onRefresh ();
	}

	private native void n_onRefresh ();

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
