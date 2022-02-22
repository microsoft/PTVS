package mono.android.support.v4.media.session;


public class MediaSessionCompat_OnActiveChangeListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		android.support.v4.media.session.MediaSessionCompat.OnActiveChangeListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onActiveChanged:()V:GetOnActiveChangedHandler:Android.Support.V4.Media.Session.MediaSessionCompat/IOnActiveChangeListenerInvoker, Xamarin.AndroidX.Media\n" +
			"";
		mono.android.Runtime.register ("Android.Support.V4.Media.Session.MediaSessionCompat+IOnActiveChangeListenerImplementor, Xamarin.AndroidX.Media", MediaSessionCompat_OnActiveChangeListenerImplementor.class, __md_methods);
	}


	public MediaSessionCompat_OnActiveChangeListenerImplementor ()
	{
		super ();
		if (getClass () == MediaSessionCompat_OnActiveChangeListenerImplementor.class)
			mono.android.TypeManager.Activate ("Android.Support.V4.Media.Session.MediaSessionCompat+IOnActiveChangeListenerImplementor, Xamarin.AndroidX.Media", "", this, new java.lang.Object[] {  });
	}


	public void onActiveChanged ()
	{
		n_onActiveChanged ();
	}

	private native void n_onActiveChanged ();

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
