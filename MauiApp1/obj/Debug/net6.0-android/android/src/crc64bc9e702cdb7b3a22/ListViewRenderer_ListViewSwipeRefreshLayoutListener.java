package crc64bc9e702cdb7b3a22;


public class ListViewRenderer_ListViewSwipeRefreshLayoutListener
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
		mono.android.Runtime.register ("Microsoft.Maui.Controls.Handlers.Compatibility.ListViewRenderer+ListViewSwipeRefreshLayoutListener, Microsoft.Maui.Controls.Compatibility", ListViewRenderer_ListViewSwipeRefreshLayoutListener.class, __md_methods);
	}


	public ListViewRenderer_ListViewSwipeRefreshLayoutListener ()
	{
		super ();
		if (getClass () == ListViewRenderer_ListViewSwipeRefreshLayoutListener.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Handlers.Compatibility.ListViewRenderer+ListViewSwipeRefreshLayoutListener, Microsoft.Maui.Controls.Compatibility", "", this, new java.lang.Object[] {  });
	}

	public ListViewRenderer_ListViewSwipeRefreshLayoutListener (crc64bc9e702cdb7b3a22.ListViewRenderer p0)
	{
		super ();
		if (getClass () == ListViewRenderer_ListViewSwipeRefreshLayoutListener.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Handlers.Compatibility.ListViewRenderer+ListViewSwipeRefreshLayoutListener, Microsoft.Maui.Controls.Compatibility", "Microsoft.Maui.Controls.Handlers.Compatibility.ListViewRenderer, Microsoft.Maui.Controls.Compatibility", this, new java.lang.Object[] { p0 });
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
