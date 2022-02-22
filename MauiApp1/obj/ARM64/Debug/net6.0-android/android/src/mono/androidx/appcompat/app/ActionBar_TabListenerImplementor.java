package mono.androidx.appcompat.app;


public class ActionBar_TabListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		androidx.appcompat.app.ActionBar.TabListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onTabReselected:(Landroidx/appcompat/app/ActionBar$Tab;Landroidx/fragment/app/FragmentTransaction;)V:GetOnTabReselected_Landroidx_appcompat_app_ActionBar_Tab_Landroidx_fragment_app_FragmentTransaction_Handler:AndroidX.AppCompat.App.ActionBar/ITabListenerInvoker, Xamarin.AndroidX.AppCompat\n" +
			"n_onTabSelected:(Landroidx/appcompat/app/ActionBar$Tab;Landroidx/fragment/app/FragmentTransaction;)V:GetOnTabSelected_Landroidx_appcompat_app_ActionBar_Tab_Landroidx_fragment_app_FragmentTransaction_Handler:AndroidX.AppCompat.App.ActionBar/ITabListenerInvoker, Xamarin.AndroidX.AppCompat\n" +
			"n_onTabUnselected:(Landroidx/appcompat/app/ActionBar$Tab;Landroidx/fragment/app/FragmentTransaction;)V:GetOnTabUnselected_Landroidx_appcompat_app_ActionBar_Tab_Landroidx_fragment_app_FragmentTransaction_Handler:AndroidX.AppCompat.App.ActionBar/ITabListenerInvoker, Xamarin.AndroidX.AppCompat\n" +
			"";
		mono.android.Runtime.register ("AndroidX.AppCompat.App.ActionBar+ITabListenerImplementor, Xamarin.AndroidX.AppCompat", ActionBar_TabListenerImplementor.class, __md_methods);
	}


	public ActionBar_TabListenerImplementor ()
	{
		super ();
		if (getClass () == ActionBar_TabListenerImplementor.class)
			mono.android.TypeManager.Activate ("AndroidX.AppCompat.App.ActionBar+ITabListenerImplementor, Xamarin.AndroidX.AppCompat", "", this, new java.lang.Object[] {  });
	}


	public void onTabReselected (androidx.appcompat.app.ActionBar.Tab p0, androidx.fragment.app.FragmentTransaction p1)
	{
		n_onTabReselected (p0, p1);
	}

	private native void n_onTabReselected (androidx.appcompat.app.ActionBar.Tab p0, androidx.fragment.app.FragmentTransaction p1);


	public void onTabSelected (androidx.appcompat.app.ActionBar.Tab p0, androidx.fragment.app.FragmentTransaction p1)
	{
		n_onTabSelected (p0, p1);
	}

	private native void n_onTabSelected (androidx.appcompat.app.ActionBar.Tab p0, androidx.fragment.app.FragmentTransaction p1);


	public void onTabUnselected (androidx.appcompat.app.ActionBar.Tab p0, androidx.fragment.app.FragmentTransaction p1)
	{
		n_onTabUnselected (p0, p1);
	}

	private native void n_onTabUnselected (androidx.appcompat.app.ActionBar.Tab p0, androidx.fragment.app.FragmentTransaction p1);

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
