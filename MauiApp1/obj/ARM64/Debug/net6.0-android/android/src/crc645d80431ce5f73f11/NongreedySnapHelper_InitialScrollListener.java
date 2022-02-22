package crc645d80431ce5f73f11;


public class NongreedySnapHelper_InitialScrollListener
	extends androidx.recyclerview.widget.RecyclerView.OnScrollListener
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onScrolled:(Landroidx/recyclerview/widget/RecyclerView;II)V:GetOnScrolled_Landroidx_recyclerview_widget_RecyclerView_IIHandler\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Controls.Handlers.Items.NongreedySnapHelper+InitialScrollListener, Microsoft.Maui.Controls", NongreedySnapHelper_InitialScrollListener.class, __md_methods);
	}


	public NongreedySnapHelper_InitialScrollListener ()
	{
		super ();
		if (getClass () == NongreedySnapHelper_InitialScrollListener.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Handlers.Items.NongreedySnapHelper+InitialScrollListener, Microsoft.Maui.Controls", "", this, new java.lang.Object[] {  });
	}

	public NongreedySnapHelper_InitialScrollListener (crc645d80431ce5f73f11.NongreedySnapHelper p0)
	{
		super ();
		if (getClass () == NongreedySnapHelper_InitialScrollListener.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Handlers.Items.NongreedySnapHelper+InitialScrollListener, Microsoft.Maui.Controls", "Microsoft.Maui.Controls.Handlers.Items.NongreedySnapHelper, Microsoft.Maui.Controls", this, new java.lang.Object[] { p0 });
	}


	public void onScrolled (androidx.recyclerview.widget.RecyclerView p0, int p1, int p2)
	{
		n_onScrolled (p0, p1, p2);
	}

	private native void n_onScrolled (androidx.recyclerview.widget.RecyclerView p0, int p1, int p2);

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
