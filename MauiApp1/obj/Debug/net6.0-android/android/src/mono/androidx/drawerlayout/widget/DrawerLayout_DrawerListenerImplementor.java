package mono.androidx.drawerlayout.widget;


public class DrawerLayout_DrawerListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		androidx.drawerlayout.widget.DrawerLayout.DrawerListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onDrawerClosed:(Landroid/view/View;)V:GetOnDrawerClosed_Landroid_view_View_Handler:AndroidX.DrawerLayout.Widget.DrawerLayout/IDrawerListenerInvoker, Xamarin.AndroidX.DrawerLayout\n" +
			"n_onDrawerOpened:(Landroid/view/View;)V:GetOnDrawerOpened_Landroid_view_View_Handler:AndroidX.DrawerLayout.Widget.DrawerLayout/IDrawerListenerInvoker, Xamarin.AndroidX.DrawerLayout\n" +
			"n_onDrawerSlide:(Landroid/view/View;F)V:GetOnDrawerSlide_Landroid_view_View_FHandler:AndroidX.DrawerLayout.Widget.DrawerLayout/IDrawerListenerInvoker, Xamarin.AndroidX.DrawerLayout\n" +
			"n_onDrawerStateChanged:(I)V:GetOnDrawerStateChanged_IHandler:AndroidX.DrawerLayout.Widget.DrawerLayout/IDrawerListenerInvoker, Xamarin.AndroidX.DrawerLayout\n" +
			"";
		mono.android.Runtime.register ("AndroidX.DrawerLayout.Widget.DrawerLayout+IDrawerListenerImplementor, Xamarin.AndroidX.DrawerLayout", DrawerLayout_DrawerListenerImplementor.class, __md_methods);
	}


	public DrawerLayout_DrawerListenerImplementor ()
	{
		super ();
		if (getClass () == DrawerLayout_DrawerListenerImplementor.class)
			mono.android.TypeManager.Activate ("AndroidX.DrawerLayout.Widget.DrawerLayout+IDrawerListenerImplementor, Xamarin.AndroidX.DrawerLayout", "", this, new java.lang.Object[] {  });
	}


	public void onDrawerClosed (android.view.View p0)
	{
		n_onDrawerClosed (p0);
	}

	private native void n_onDrawerClosed (android.view.View p0);


	public void onDrawerOpened (android.view.View p0)
	{
		n_onDrawerOpened (p0);
	}

	private native void n_onDrawerOpened (android.view.View p0);


	public void onDrawerSlide (android.view.View p0, float p1)
	{
		n_onDrawerSlide (p0, p1);
	}

	private native void n_onDrawerSlide (android.view.View p0, float p1);


	public void onDrawerStateChanged (int p0)
	{
		n_onDrawerStateChanged (p0);
	}

	private native void n_onDrawerStateChanged (int p0);

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
