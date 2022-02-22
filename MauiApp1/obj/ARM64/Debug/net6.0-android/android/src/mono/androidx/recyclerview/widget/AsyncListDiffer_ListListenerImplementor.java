package mono.androidx.recyclerview.widget;


public class AsyncListDiffer_ListListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		androidx.recyclerview.widget.AsyncListDiffer.ListListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onCurrentListChanged:(Ljava/util/List;Ljava/util/List;)V:GetOnCurrentListChanged_Ljava_util_List_Ljava_util_List_Handler:AndroidX.RecyclerView.Widget.AsyncListDiffer/IListListenerInvoker, Xamarin.AndroidX.RecyclerView\n" +
			"";
		mono.android.Runtime.register ("AndroidX.RecyclerView.Widget.AsyncListDiffer+IListListenerImplementor, Xamarin.AndroidX.RecyclerView", AsyncListDiffer_ListListenerImplementor.class, __md_methods);
	}


	public AsyncListDiffer_ListListenerImplementor ()
	{
		super ();
		if (getClass () == AsyncListDiffer_ListListenerImplementor.class)
			mono.android.TypeManager.Activate ("AndroidX.RecyclerView.Widget.AsyncListDiffer+IListListenerImplementor, Xamarin.AndroidX.RecyclerView", "", this, new java.lang.Object[] {  });
	}


	public void onCurrentListChanged (java.util.List p0, java.util.List p1)
	{
		n_onCurrentListChanged (p0, p1);
	}

	private native void n_onCurrentListChanged (java.util.List p0, java.util.List p1);

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
