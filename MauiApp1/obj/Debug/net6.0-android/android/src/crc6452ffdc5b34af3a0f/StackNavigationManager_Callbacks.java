package crc6452ffdc5b34af3a0f;


public class StackNavigationManager_Callbacks
	extends androidx.fragment.app.FragmentManager.FragmentLifecycleCallbacks
	implements
		mono.android.IGCUserPeer,
		androidx.navigation.NavController.OnDestinationChangedListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onFragmentResumed:(Landroidx/fragment/app/FragmentManager;Landroidx/fragment/app/Fragment;)V:GetOnFragmentResumed_Landroidx_fragment_app_FragmentManager_Landroidx_fragment_app_Fragment_Handler\n" +
			"n_onFragmentViewDestroyed:(Landroidx/fragment/app/FragmentManager;Landroidx/fragment/app/Fragment;)V:GetOnFragmentViewDestroyed_Landroidx_fragment_app_FragmentManager_Landroidx_fragment_app_Fragment_Handler\n" +
			"n_onDestinationChanged:(Landroidx/navigation/NavController;Landroidx/navigation/NavDestination;Landroid/os/Bundle;)V:GetOnDestinationChanged_Landroidx_navigation_NavController_Landroidx_navigation_NavDestination_Landroid_os_Bundle_Handler:AndroidX.Navigation.NavController/IOnDestinationChangedListenerInvoker, Xamarin.AndroidX.Navigation.Runtime\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Platform.StackNavigationManager+Callbacks, Microsoft.Maui", StackNavigationManager_Callbacks.class, __md_methods);
	}


	public StackNavigationManager_Callbacks ()
	{
		super ();
		if (getClass () == StackNavigationManager_Callbacks.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Platform.StackNavigationManager+Callbacks, Microsoft.Maui", "", this, new java.lang.Object[] {  });
	}


	public void onFragmentResumed (androidx.fragment.app.FragmentManager p0, androidx.fragment.app.Fragment p1)
	{
		n_onFragmentResumed (p0, p1);
	}

	private native void n_onFragmentResumed (androidx.fragment.app.FragmentManager p0, androidx.fragment.app.Fragment p1);


	public void onFragmentViewDestroyed (androidx.fragment.app.FragmentManager p0, androidx.fragment.app.Fragment p1)
	{
		n_onFragmentViewDestroyed (p0, p1);
	}

	private native void n_onFragmentViewDestroyed (androidx.fragment.app.FragmentManager p0, androidx.fragment.app.Fragment p1);


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
