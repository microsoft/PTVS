package crc6413a2ffd8f229512a;


public class CarouselViewAdapter_2
	extends crc6477f0d89a9cfd64b1.ItemsViewAdapter_2
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_getItemCount:()I:GetGetItemCountHandler\n" +
			"n_onBindViewHolder:(Landroidx/recyclerview/widget/RecyclerView$ViewHolder;I)V:GetOnBindViewHolder_Landroidx_recyclerview_widget_RecyclerView_ViewHolder_IHandler\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Controls.Compatibility.Platform.Android.CollectionView.CarouselViewAdapter`2, Microsoft.Maui.Controls.Compatibility", CarouselViewAdapter_2.class, __md_methods);
	}


	public CarouselViewAdapter_2 ()
	{
		super ();
		if (getClass () == CarouselViewAdapter_2.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.CollectionView.CarouselViewAdapter`2, Microsoft.Maui.Controls.Compatibility", "", this, new java.lang.Object[] {  });
	}


	public int getItemCount ()
	{
		return n_getItemCount ();
	}

	private native int n_getItemCount ();


	public void onBindViewHolder (androidx.recyclerview.widget.RecyclerView.ViewHolder p0, int p1)
	{
		n_onBindViewHolder (p0, p1);
	}

	private native void n_onBindViewHolder (androidx.recyclerview.widget.RecyclerView.ViewHolder p0, int p1);

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
