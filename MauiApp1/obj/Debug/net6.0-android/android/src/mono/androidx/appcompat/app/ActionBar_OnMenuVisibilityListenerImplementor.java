package mono.androidx.appcompat.app;


public class ActionBar_OnMenuVisibilityListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		androidx.appcompat.app.ActionBar.OnMenuVisibilityListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onMenuVisibilityChanged:(Z)V:GetOnMenuVisibilityChanged_ZHandler:AndroidX.AppCompat.App.ActionBar/IOnMenuVisibilityListenerInvoker, Xamarin.AndroidX.AppCompat\n" +
			"";
		mono.android.Runtime.register ("AndroidX.AppCompat.App.ActionBar+IOnMenuVisibilityListenerImplementor, Xamarin.AndroidX.AppCompat", ActionBar_OnMenuVisibilityListenerImplementor.class, __md_methods);
	}


	public ActionBar_OnMenuVisibilityListenerImplementor ()
	{
		super ();
		if (getClass () == ActionBar_OnMenuVisibilityListenerImplementor.class)
			mono.android.TypeManager.Activate ("AndroidX.AppCompat.App.ActionBar+IOnMenuVisibilityListenerImplementor, Xamarin.AndroidX.AppCompat", "", this, new java.lang.Object[] {  });
	}


	public void onMenuVisibilityChanged (boolean p0)
	{
		n_onMenuVisibilityChanged (p0);
	}

	private native void n_onMenuVisibilityChanged (boolean p0);

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
