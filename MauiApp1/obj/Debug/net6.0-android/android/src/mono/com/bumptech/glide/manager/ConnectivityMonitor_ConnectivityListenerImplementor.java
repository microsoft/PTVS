package mono.com.bumptech.glide.manager;


public class ConnectivityMonitor_ConnectivityListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		com.bumptech.glide.manager.ConnectivityMonitor.ConnectivityListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onConnectivityChanged:(Z)V:GetOnConnectivityChanged_ZHandler:Bumptech.Glide.Manager.IConnectivityMonitorConnectivityListenerInvoker, Xamarin.Android.Glide\n" +
			"";
		mono.android.Runtime.register ("Bumptech.Glide.Manager.IConnectivityMonitorConnectivityListenerImplementor, Xamarin.Android.Glide", ConnectivityMonitor_ConnectivityListenerImplementor.class, __md_methods);
	}


	public ConnectivityMonitor_ConnectivityListenerImplementor ()
	{
		super ();
		if (getClass () == ConnectivityMonitor_ConnectivityListenerImplementor.class)
			mono.android.TypeManager.Activate ("Bumptech.Glide.Manager.IConnectivityMonitorConnectivityListenerImplementor, Xamarin.Android.Glide", "", this, new java.lang.Object[] {  });
	}


	public void onConnectivityChanged (boolean p0)
	{
		n_onConnectivityChanged (p0);
	}

	private native void n_onConnectivityChanged (boolean p0);

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
