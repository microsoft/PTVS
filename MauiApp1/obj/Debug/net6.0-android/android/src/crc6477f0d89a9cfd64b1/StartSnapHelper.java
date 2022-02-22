package crc6477f0d89a9cfd64b1;


public class StartSnapHelper
	extends crc6477f0d89a9cfd64b1.EdgeSnapHelper
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_calculateDistanceToFinalSnap:(Landroidx/recyclerview/widget/RecyclerView$LayoutManager;Landroid/view/View;)[I:GetCalculateDistanceToFinalSnap_Landroidx_recyclerview_widget_RecyclerView_LayoutManager_Landroid_view_View_Handler\n" +
			"n_findSnapView:(Landroidx/recyclerview/widget/RecyclerView$LayoutManager;)Landroid/view/View;:GetFindSnapView_Landroidx_recyclerview_widget_RecyclerView_LayoutManager_Handler\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Controls.Compatibility.Platform.Android.StartSnapHelper, Microsoft.Maui.Controls.Compatibility", StartSnapHelper.class, __md_methods);
	}


	public StartSnapHelper ()
	{
		super ();
		if (getClass () == StartSnapHelper.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.StartSnapHelper, Microsoft.Maui.Controls.Compatibility", "", this, new java.lang.Object[] {  });
	}


	public int[] calculateDistanceToFinalSnap (androidx.recyclerview.widget.RecyclerView.LayoutManager p0, android.view.View p1)
	{
		return n_calculateDistanceToFinalSnap (p0, p1);
	}

	private native int[] n_calculateDistanceToFinalSnap (androidx.recyclerview.widget.RecyclerView.LayoutManager p0, android.view.View p1);


	public android.view.View findSnapView (androidx.recyclerview.widget.RecyclerView.LayoutManager p0)
	{
		return n_findSnapView (p0);
	}

	private native android.view.View n_findSnapView (androidx.recyclerview.widget.RecyclerView.LayoutManager p0);

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
