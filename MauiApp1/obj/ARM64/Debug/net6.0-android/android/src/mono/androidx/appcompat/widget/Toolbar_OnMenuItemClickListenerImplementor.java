package mono.androidx.appcompat.widget;


public class Toolbar_OnMenuItemClickListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		androidx.appcompat.widget.Toolbar.OnMenuItemClickListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onMenuItemClick:(Landroid/view/MenuItem;)Z:GetOnMenuItemClick_Landroid_view_MenuItem_Handler:AndroidX.AppCompat.Widget.Toolbar/IOnMenuItemClickListenerInvoker, Xamarin.AndroidX.AppCompat\n" +
			"";
		mono.android.Runtime.register ("AndroidX.AppCompat.Widget.Toolbar+IOnMenuItemClickListenerImplementor, Xamarin.AndroidX.AppCompat", Toolbar_OnMenuItemClickListenerImplementor.class, __md_methods);
	}


	public Toolbar_OnMenuItemClickListenerImplementor ()
	{
		super ();
		if (getClass () == Toolbar_OnMenuItemClickListenerImplementor.class)
			mono.android.TypeManager.Activate ("AndroidX.AppCompat.Widget.Toolbar+IOnMenuItemClickListenerImplementor, Xamarin.AndroidX.AppCompat", "", this, new java.lang.Object[] {  });
	}


	public boolean onMenuItemClick (android.view.MenuItem p0)
	{
		return n_onMenuItemClick (p0);
	}

	private native boolean n_onMenuItemClick (android.view.MenuItem p0);

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
