package mono.androidx.navigation.ui;


public class AppBarConfiguration_OnNavigateUpListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		androidx.navigation.ui.AppBarConfiguration.OnNavigateUpListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onNavigateUp:()Z:GetOnNavigateUpHandler:AndroidX.Navigation.UI.AppBarConfiguration/IOnNavigateUpListenerInvoker, Xamarin.AndroidX.Navigation.UI\n" +
			"";
		mono.android.Runtime.register ("AndroidX.Navigation.UI.AppBarConfiguration+IOnNavigateUpListenerImplementor, Xamarin.AndroidX.Navigation.UI", AppBarConfiguration_OnNavigateUpListenerImplementor.class, __md_methods);
	}


	public AppBarConfiguration_OnNavigateUpListenerImplementor ()
	{
		super ();
		if (getClass () == AppBarConfiguration_OnNavigateUpListenerImplementor.class)
			mono.android.TypeManager.Activate ("AndroidX.Navigation.UI.AppBarConfiguration+IOnNavigateUpListenerImplementor, Xamarin.AndroidX.Navigation.UI", "", this, new java.lang.Object[] {  });
	}


	public boolean onNavigateUp ()
	{
		return n_onNavigateUp ();
	}

	private native boolean n_onNavigateUp ();

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
