package androidx.appcompat.widget;


public class Toolbar_NavigationOnClickEventDispatcher
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		android.view.View.OnClickListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onClick:(Landroid/view/View;)V:GetOnClick_Landroid_view_View_Handler:Android.Views.View/IOnClickListenerInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"";
		mono.android.Runtime.register ("AndroidX.AppCompat.Widget.Toolbar+NavigationOnClickEventDispatcher, Xamarin.AndroidX.AppCompat", Toolbar_NavigationOnClickEventDispatcher.class, __md_methods);
	}


	public Toolbar_NavigationOnClickEventDispatcher ()
	{
		super ();
		if (getClass () == Toolbar_NavigationOnClickEventDispatcher.class)
			mono.android.TypeManager.Activate ("AndroidX.AppCompat.Widget.Toolbar+NavigationOnClickEventDispatcher, Xamarin.AndroidX.AppCompat", "", this, new java.lang.Object[] {  });
	}

	public Toolbar_NavigationOnClickEventDispatcher (androidx.appcompat.widget.Toolbar p0)
	{
		super ();
		if (getClass () == Toolbar_NavigationOnClickEventDispatcher.class)
			mono.android.TypeManager.Activate ("AndroidX.AppCompat.Widget.Toolbar+NavigationOnClickEventDispatcher, Xamarin.AndroidX.AppCompat", "AndroidX.AppCompat.Widget.Toolbar, Xamarin.AndroidX.AppCompat", this, new java.lang.Object[] { p0 });
	}


	public void onClick (android.view.View p0)
	{
		n_onClick (p0);
	}

	private native void n_onClick (android.view.View p0);

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
