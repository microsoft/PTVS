package crc6477f0d89a9cfd64b1;


public class ListViewRenderer_SwipeRefreshLayoutWithFixedNestedScrolling
	extends androidx.swiperefreshlayout.widget.SwipeRefreshLayout
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onInterceptTouchEvent:(Landroid/view/MotionEvent;)Z:GetOnInterceptTouchEvent_Landroid_view_MotionEvent_Handler\n" +
			"n_onNestedScrollAccepted:(Landroid/view/View;Landroid/view/View;I)V:GetOnNestedScrollAccepted_Landroid_view_View_Landroid_view_View_IHandler\n" +
			"n_onStopNestedScroll:(Landroid/view/View;)V:GetOnStopNestedScroll_Landroid_view_View_Handler\n" +
			"n_onNestedScroll:(Landroid/view/View;IIII)V:GetOnNestedScroll_Landroid_view_View_IIIIHandler\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Controls.Compatibility.Platform.Android.ListViewRenderer+SwipeRefreshLayoutWithFixedNestedScrolling, Microsoft.Maui.Controls.Compatibility", ListViewRenderer_SwipeRefreshLayoutWithFixedNestedScrolling.class, __md_methods);
	}


	public ListViewRenderer_SwipeRefreshLayoutWithFixedNestedScrolling (android.content.Context p0)
	{
		super (p0);
		if (getClass () == ListViewRenderer_SwipeRefreshLayoutWithFixedNestedScrolling.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.ListViewRenderer+SwipeRefreshLayoutWithFixedNestedScrolling, Microsoft.Maui.Controls.Compatibility", "Android.Content.Context, Mono.Android", this, new java.lang.Object[] { p0 });
	}


	public ListViewRenderer_SwipeRefreshLayoutWithFixedNestedScrolling (android.content.Context p0, android.util.AttributeSet p1)
	{
		super (p0, p1);
		if (getClass () == ListViewRenderer_SwipeRefreshLayoutWithFixedNestedScrolling.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.ListViewRenderer+SwipeRefreshLayoutWithFixedNestedScrolling, Microsoft.Maui.Controls.Compatibility", "Android.Content.Context, Mono.Android:Android.Util.IAttributeSet, Mono.Android", this, new java.lang.Object[] { p0, p1 });
	}


	public boolean onInterceptTouchEvent (android.view.MotionEvent p0)
	{
		return n_onInterceptTouchEvent (p0);
	}

	private native boolean n_onInterceptTouchEvent (android.view.MotionEvent p0);


	public void onNestedScrollAccepted (android.view.View p0, android.view.View p1, int p2)
	{
		n_onNestedScrollAccepted (p0, p1, p2);
	}

	private native void n_onNestedScrollAccepted (android.view.View p0, android.view.View p1, int p2);


	public void onStopNestedScroll (android.view.View p0)
	{
		n_onStopNestedScroll (p0);
	}

	private native void n_onStopNestedScroll (android.view.View p0);


	public void onNestedScroll (android.view.View p0, int p1, int p2, int p3, int p4)
	{
		n_onNestedScroll (p0, p1, p2, p3, p4);
	}

	private native void n_onNestedScroll (android.view.View p0, int p1, int p2, int p3, int p4);

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
