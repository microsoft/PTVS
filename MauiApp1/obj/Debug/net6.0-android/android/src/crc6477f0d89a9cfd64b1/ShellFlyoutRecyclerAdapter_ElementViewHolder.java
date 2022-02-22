package crc6477f0d89a9cfd64b1;


public class ShellFlyoutRecyclerAdapter_ElementViewHolder
	extends androidx.recyclerview.widget.RecyclerView.ViewHolder
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Controls.Compatibility.Platform.Android.ShellFlyoutRecyclerAdapter+ElementViewHolder, Microsoft.Maui.Controls.Compatibility", ShellFlyoutRecyclerAdapter_ElementViewHolder.class, __md_methods);
	}


	public ShellFlyoutRecyclerAdapter_ElementViewHolder (android.view.View p0)
	{
		super (p0);
		if (getClass () == ShellFlyoutRecyclerAdapter_ElementViewHolder.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.ShellFlyoutRecyclerAdapter+ElementViewHolder, Microsoft.Maui.Controls.Compatibility", "Android.Views.View, Mono.Android", this, new java.lang.Object[] { p0 });
	}

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
