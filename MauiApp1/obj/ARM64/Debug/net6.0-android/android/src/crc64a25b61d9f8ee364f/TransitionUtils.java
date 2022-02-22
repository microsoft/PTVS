package crc64a25b61d9f8ee364f;


public class TransitionUtils
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"";
		mono.android.Runtime.register ("AndroidX.Transitions.TransitionUtils, Xamarin.AndroidX.Transition", TransitionUtils.class, __md_methods);
	}


	public TransitionUtils ()
	{
		super ();
		if (getClass () == TransitionUtils.class)
			mono.android.TypeManager.Activate ("AndroidX.Transitions.TransitionUtils, Xamarin.AndroidX.Transition", "", this, new java.lang.Object[] {  });
	}

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
