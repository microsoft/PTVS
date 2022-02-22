package mono.androidx.viewpager.widget;


public class ViewPager_OnAdapterChangeListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		androidx.viewpager.widget.ViewPager.OnAdapterChangeListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onAdapterChanged:(Landroidx/viewpager/widget/ViewPager;Landroidx/viewpager/widget/PagerAdapter;Landroidx/viewpager/widget/PagerAdapter;)V:GetOnAdapterChanged_Landroidx_viewpager_widget_ViewPager_Landroidx_viewpager_widget_PagerAdapter_Landroidx_viewpager_widget_PagerAdapter_Handler:AndroidX.ViewPager.Widget.ViewPager/IOnAdapterChangeListenerInvoker, Xamarin.AndroidX.ViewPager\n" +
			"";
		mono.android.Runtime.register ("AndroidX.ViewPager.Widget.ViewPager+IOnAdapterChangeListenerImplementor, Xamarin.AndroidX.ViewPager", ViewPager_OnAdapterChangeListenerImplementor.class, __md_methods);
	}


	public ViewPager_OnAdapterChangeListenerImplementor ()
	{
		super ();
		if (getClass () == ViewPager_OnAdapterChangeListenerImplementor.class)
			mono.android.TypeManager.Activate ("AndroidX.ViewPager.Widget.ViewPager+IOnAdapterChangeListenerImplementor, Xamarin.AndroidX.ViewPager", "", this, new java.lang.Object[] {  });
	}


	public void onAdapterChanged (androidx.viewpager.widget.ViewPager p0, androidx.viewpager.widget.PagerAdapter p1, androidx.viewpager.widget.PagerAdapter p2)
	{
		n_onAdapterChanged (p0, p1, p2);
	}

	private native void n_onAdapterChanged (androidx.viewpager.widget.ViewPager p0, androidx.viewpager.widget.PagerAdapter p1, androidx.viewpager.widget.PagerAdapter p2);

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
