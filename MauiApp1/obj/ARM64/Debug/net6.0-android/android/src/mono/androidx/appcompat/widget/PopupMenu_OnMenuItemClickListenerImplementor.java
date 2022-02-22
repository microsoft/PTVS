package mono.androidx.appcompat.widget;


public class PopupMenu_OnMenuItemClickListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		androidx.appcompat.widget.PopupMenu.OnMenuItemClickListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onMenuItemClick:(Landroid/view/MenuItem;)Z:GetOnMenuItemClick_Landroid_view_MenuItem_Handler:AndroidX.AppCompat.Widget.PopupMenu/IOnMenuItemClickListenerInvoker, Xamarin.AndroidX.AppCompat\n" +
			"";
		mono.android.Runtime.register ("AndroidX.AppCompat.Widget.PopupMenu+IOnMenuItemClickListenerImplementor, Xamarin.AndroidX.AppCompat", PopupMenu_OnMenuItemClickListenerImplementor.class, __md_methods);
	}


	public PopupMenu_OnMenuItemClickListenerImplementor ()
	{
		super ();
		if (getClass () == PopupMenu_OnMenuItemClickListenerImplementor.class)
			mono.android.TypeManager.Activate ("AndroidX.AppCompat.Widget.PopupMenu+IOnMenuItemClickListenerImplementor, Xamarin.AndroidX.AppCompat", "", this, new java.lang.Object[] {  });
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
