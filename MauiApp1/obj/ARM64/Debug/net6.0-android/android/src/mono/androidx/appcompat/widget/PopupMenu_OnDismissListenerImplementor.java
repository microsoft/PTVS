package mono.androidx.appcompat.widget;


public class PopupMenu_OnDismissListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		androidx.appcompat.widget.PopupMenu.OnDismissListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onDismiss:(Landroidx/appcompat/widget/PopupMenu;)V:GetOnDismiss_Landroidx_appcompat_widget_PopupMenu_Handler:AndroidX.AppCompat.Widget.PopupMenu/IOnDismissListenerInvoker, Xamarin.AndroidX.AppCompat\n" +
			"";
		mono.android.Runtime.register ("AndroidX.AppCompat.Widget.PopupMenu+IOnDismissListenerImplementor, Xamarin.AndroidX.AppCompat", PopupMenu_OnDismissListenerImplementor.class, __md_methods);
	}


	public PopupMenu_OnDismissListenerImplementor ()
	{
		super ();
		if (getClass () == PopupMenu_OnDismissListenerImplementor.class)
			mono.android.TypeManager.Activate ("AndroidX.AppCompat.Widget.PopupMenu+IOnDismissListenerImplementor, Xamarin.AndroidX.AppCompat", "", this, new java.lang.Object[] {  });
	}


	public void onDismiss (androidx.appcompat.widget.PopupMenu p0)
	{
		n_onDismiss (p0);
	}

	private native void n_onDismiss (androidx.appcompat.widget.PopupMenu p0);

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
