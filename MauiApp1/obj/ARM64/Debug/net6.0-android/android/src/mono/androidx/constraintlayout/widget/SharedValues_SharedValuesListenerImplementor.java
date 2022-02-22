package mono.androidx.constraintlayout.widget;


public class SharedValues_SharedValuesListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		androidx.constraintlayout.widget.SharedValues.SharedValuesListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onNewValue:(III)V:GetOnNewValue_IIIHandler:AndroidX.ConstraintLayout.Widget.SharedValues/ISharedValuesListenerInvoker, Xamarin.AndroidX.ConstraintLayout\n" +
			"";
		mono.android.Runtime.register ("AndroidX.ConstraintLayout.Widget.SharedValues+ISharedValuesListenerImplementor, Xamarin.AndroidX.ConstraintLayout", SharedValues_SharedValuesListenerImplementor.class, __md_methods);
	}


	public SharedValues_SharedValuesListenerImplementor ()
	{
		super ();
		if (getClass () == SharedValues_SharedValuesListenerImplementor.class)
			mono.android.TypeManager.Activate ("AndroidX.ConstraintLayout.Widget.SharedValues+ISharedValuesListenerImplementor, Xamarin.AndroidX.ConstraintLayout", "", this, new java.lang.Object[] {  });
	}


	public void onNewValue (int p0, int p1, int p2)
	{
		n_onNewValue (p0, p1, p2);
	}

	private native void n_onNewValue (int p0, int p1, int p2);

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
