package mono.androidx.appcompat.widget;


public class FitWindowsViewGroup_OnFitSystemWindowsListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		androidx.appcompat.widget.FitWindowsViewGroup.OnFitSystemWindowsListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onFitSystemWindows:(Landroid/graphics/Rect;)V:GetOnFitSystemWindows_Landroid_graphics_Rect_Handler:AndroidX.AppCompat.Widget.IFitWindowsViewGroupOnFitSystemWindowsListenerInvoker, Xamarin.AndroidX.AppCompat\n" +
			"";
		mono.android.Runtime.register ("AndroidX.AppCompat.Widget.IFitWindowsViewGroupOnFitSystemWindowsListenerImplementor, Xamarin.AndroidX.AppCompat", FitWindowsViewGroup_OnFitSystemWindowsListenerImplementor.class, __md_methods);
	}


	public FitWindowsViewGroup_OnFitSystemWindowsListenerImplementor ()
	{
		super ();
		if (getClass () == FitWindowsViewGroup_OnFitSystemWindowsListenerImplementor.class)
			mono.android.TypeManager.Activate ("AndroidX.AppCompat.Widget.IFitWindowsViewGroupOnFitSystemWindowsListenerImplementor, Xamarin.AndroidX.AppCompat", "", this, new java.lang.Object[] {  });
	}


	public void onFitSystemWindows (android.graphics.Rect p0)
	{
		n_onFitSystemWindows (p0);
	}

	private native void n_onFitSystemWindows (android.graphics.Rect p0);

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
