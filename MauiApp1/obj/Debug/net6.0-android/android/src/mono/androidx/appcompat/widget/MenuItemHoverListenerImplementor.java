package mono.androidx.appcompat.widget;


public class MenuItemHoverListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		androidx.appcompat.widget.MenuItemHoverListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onItemHoverEnter:(Landroidx/appcompat/view/menu/MenuBuilder;Landroid/view/MenuItem;)V:GetOnItemHoverEnter_Landroidx_appcompat_view_menu_MenuBuilder_Landroid_view_MenuItem_Handler:AndroidX.AppCompat.Widget.IMenuItemHoverListenerInvoker, Xamarin.AndroidX.AppCompat\n" +
			"n_onItemHoverExit:(Landroidx/appcompat/view/menu/MenuBuilder;Landroid/view/MenuItem;)V:GetOnItemHoverExit_Landroidx_appcompat_view_menu_MenuBuilder_Landroid_view_MenuItem_Handler:AndroidX.AppCompat.Widget.IMenuItemHoverListenerInvoker, Xamarin.AndroidX.AppCompat\n" +
			"";
		mono.android.Runtime.register ("AndroidX.AppCompat.Widget.IMenuItemHoverListenerImplementor, Xamarin.AndroidX.AppCompat", MenuItemHoverListenerImplementor.class, __md_methods);
	}


	public MenuItemHoverListenerImplementor ()
	{
		super ();
		if (getClass () == MenuItemHoverListenerImplementor.class)
			mono.android.TypeManager.Activate ("AndroidX.AppCompat.Widget.IMenuItemHoverListenerImplementor, Xamarin.AndroidX.AppCompat", "", this, new java.lang.Object[] {  });
	}


	public void onItemHoverEnter (androidx.appcompat.view.menu.MenuBuilder p0, android.view.MenuItem p1)
	{
		n_onItemHoverEnter (p0, p1);
	}

	private native void n_onItemHoverEnter (androidx.appcompat.view.menu.MenuBuilder p0, android.view.MenuItem p1);


	public void onItemHoverExit (androidx.appcompat.view.menu.MenuBuilder p0, android.view.MenuItem p1)
	{
		n_onItemHoverExit (p0, p1);
	}

	private native void n_onItemHoverExit (androidx.appcompat.view.menu.MenuBuilder p0, android.view.MenuItem p1);

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
