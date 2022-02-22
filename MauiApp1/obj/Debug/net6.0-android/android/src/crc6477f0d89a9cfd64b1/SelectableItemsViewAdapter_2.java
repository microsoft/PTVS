package crc6477f0d89a9cfd64b1;


public class SelectableItemsViewAdapter_2
	extends crc6477f0d89a9cfd64b1.StructuredItemsViewAdapter_2
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onBindViewHolder:(Landroidx/recyclerview/widget/RecyclerView$ViewHolder;I)V:GetOnBindViewHolder_Landroidx_recyclerview_widget_RecyclerView_ViewHolder_IHandler\n" +
			"n_onViewRecycled:(Landroidx/recyclerview/widget/RecyclerView$ViewHolder;)V:GetOnViewRecycled_Landroidx_recyclerview_widget_RecyclerView_ViewHolder_Handler\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Controls.Compatibility.Platform.Android.SelectableItemsViewAdapter`2, Microsoft.Maui.Controls.Compatibility", SelectableItemsViewAdapter_2.class, __md_methods);
	}


	public SelectableItemsViewAdapter_2 ()
	{
		super ();
		if (getClass () == SelectableItemsViewAdapter_2.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.SelectableItemsViewAdapter`2, Microsoft.Maui.Controls.Compatibility", "", this, new java.lang.Object[] {  });
	}


	public void onBindViewHolder (androidx.recyclerview.widget.RecyclerView.ViewHolder p0, int p1)
	{
		n_onBindViewHolder (p0, p1);
	}

	private native void n_onBindViewHolder (androidx.recyclerview.widget.RecyclerView.ViewHolder p0, int p1);


	public void onViewRecycled (androidx.recyclerview.widget.RecyclerView.ViewHolder p0)
	{
		n_onViewRecycled (p0);
	}

	private native void n_onViewRecycled (androidx.recyclerview.widget.RecyclerView.ViewHolder p0);

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
