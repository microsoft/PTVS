package mono.androidx.core.view.accessibility;


public class AccessibilityManagerCompat_AccessibilityStateChangeListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		androidx.core.view.accessibility.AccessibilityManagerCompat.AccessibilityStateChangeListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onAccessibilityStateChanged:(Z)V:GetOnAccessibilityStateChanged_ZHandler:AndroidX.Core.View.Accessibility.AccessibilityManagerCompat/IAccessibilityStateChangeListenerInvoker, Xamarin.AndroidX.Core\n" +
			"";
		mono.android.Runtime.register ("AndroidX.Core.View.Accessibility.AccessibilityManagerCompat+IAccessibilityStateChangeListenerImplementor, Xamarin.AndroidX.Core", AccessibilityManagerCompat_AccessibilityStateChangeListenerImplementor.class, __md_methods);
	}


	public AccessibilityManagerCompat_AccessibilityStateChangeListenerImplementor ()
	{
		super ();
		if (getClass () == AccessibilityManagerCompat_AccessibilityStateChangeListenerImplementor.class)
			mono.android.TypeManager.Activate ("AndroidX.Core.View.Accessibility.AccessibilityManagerCompat+IAccessibilityStateChangeListenerImplementor, Xamarin.AndroidX.Core", "", this, new java.lang.Object[] {  });
	}


	public void onAccessibilityStateChanged (boolean p0)
	{
		n_onAccessibilityStateChanged (p0);
	}

	private native void n_onAccessibilityStateChanged (boolean p0);

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
