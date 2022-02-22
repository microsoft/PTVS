package mono.androidx.navigation;


public class NavController_OnDestinationChangedListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		androidx.navigation.NavController.OnDestinationChangedListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onDestinationChanged:(Landroidx/navigation/NavController;Landroidx/navigation/NavDestination;Landroid/os/Bundle;)V:GetOnDestinationChanged_Landroidx_navigation_NavController_Landroidx_navigation_NavDestination_Landroid_os_Bundle_Handler:AndroidX.Navigation.NavController/IOnDestinationChangedListenerInvoker, Xamarin.AndroidX.Navigation.Runtime\n" +
			"";
		mono.android.Runtime.register ("AndroidX.Navigation.NavController+IOnDestinationChangedListenerImplementor, Xamarin.AndroidX.Navigation.Runtime", NavController_OnDestinationChangedListenerImplementor.class, __md_methods);
	}


	public NavController_OnDestinationChangedListenerImplementor ()
	{
		super ();
		if (getClass () == NavController_OnDestinationChangedListenerImplementor.class)
			mono.android.TypeManager.Activate ("AndroidX.Navigation.NavController+IOnDestinationChangedListenerImplementor, Xamarin.AndroidX.Navigation.Runtime", "", this, new java.lang.Object[] {  });
	}


	public void onDestinationChanged (androidx.navigation.NavController p0, androidx.navigation.NavDestination p1, android.os.Bundle p2)
	{
		n_onDestinationChanged (p0, p1, p2);
	}

	private native void n_onDestinationChanged (androidx.navigation.NavController p0, androidx.navigation.NavDestination p1, android.os.Bundle p2);

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
