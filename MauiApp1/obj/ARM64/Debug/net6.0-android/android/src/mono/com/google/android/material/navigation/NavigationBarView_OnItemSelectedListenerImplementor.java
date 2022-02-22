package mono.com.google.android.material.navigation;


public class NavigationBarView_OnItemSelectedListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		com.google.android.material.navigation.NavigationBarView.OnItemSelectedListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onNavigationItemSelected:(Landroid/view/MenuItem;)Z:GetOnNavigationItemSelected_Landroid_view_MenuItem_Handler:Google.Android.Material.Navigation.NavigationBarView/IOnItemSelectedListenerInvoker, Xamarin.Google.Android.Material\n" +
			"";
		mono.android.Runtime.register ("Google.Android.Material.Navigation.NavigationBarView+IOnItemSelectedListenerImplementor, Xamarin.Google.Android.Material", NavigationBarView_OnItemSelectedListenerImplementor.class, __md_methods);
	}


	public NavigationBarView_OnItemSelectedListenerImplementor ()
	{
		super ();
		if (getClass () == NavigationBarView_OnItemSelectedListenerImplementor.class)
			mono.android.TypeManager.Activate ("Google.Android.Material.Navigation.NavigationBarView+IOnItemSelectedListenerImplementor, Xamarin.Google.Android.Material", "", this, new java.lang.Object[] {  });
	}


	public boolean onNavigationItemSelected (android.view.MenuItem p0)
	{
		return n_onNavigationItemSelected (p0);
	}

	private native boolean n_onNavigationItemSelected (android.view.MenuItem p0);

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
