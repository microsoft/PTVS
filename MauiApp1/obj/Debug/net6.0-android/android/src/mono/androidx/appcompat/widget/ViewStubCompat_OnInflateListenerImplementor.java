package mono.androidx.appcompat.widget;


public class ViewStubCompat_OnInflateListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		androidx.appcompat.widget.ViewStubCompat.OnInflateListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onInflate:(Landroidx/appcompat/widget/ViewStubCompat;Landroid/view/View;)V:GetOnInflate_Landroidx_appcompat_widget_ViewStubCompat_Landroid_view_View_Handler:AndroidX.AppCompat.Widget.ViewStubCompat/IOnInflateListenerInvoker, Xamarin.AndroidX.AppCompat\n" +
			"";
		mono.android.Runtime.register ("AndroidX.AppCompat.Widget.ViewStubCompat+IOnInflateListenerImplementor, Xamarin.AndroidX.AppCompat", ViewStubCompat_OnInflateListenerImplementor.class, __md_methods);
	}


	public ViewStubCompat_OnInflateListenerImplementor ()
	{
		super ();
		if (getClass () == ViewStubCompat_OnInflateListenerImplementor.class)
			mono.android.TypeManager.Activate ("AndroidX.AppCompat.Widget.ViewStubCompat+IOnInflateListenerImplementor, Xamarin.AndroidX.AppCompat", "", this, new java.lang.Object[] {  });
	}


	public void onInflate (androidx.appcompat.widget.ViewStubCompat p0, android.view.View p1)
	{
		n_onInflate (p0, p1);
	}

	private native void n_onInflate (androidx.appcompat.widget.ViewStubCompat p0, android.view.View p1);

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
