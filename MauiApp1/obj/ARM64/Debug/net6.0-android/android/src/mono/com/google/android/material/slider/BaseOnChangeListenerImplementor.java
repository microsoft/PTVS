package mono.com.google.android.material.slider;


public class BaseOnChangeListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		com.google.android.material.slider.BaseOnChangeListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onValueChange:(Ljava/lang/Object;FZ)V:GetOnValueChange_Ljava_lang_Object_FZHandler:Google.Android.Material.Slider.IBaseOnChangeListenerInvoker, Xamarin.Google.Android.Material\n" +
			"";
		mono.android.Runtime.register ("Google.Android.Material.Slider.IBaseOnChangeListenerImplementor, Xamarin.Google.Android.Material", BaseOnChangeListenerImplementor.class, __md_methods);
	}


	public BaseOnChangeListenerImplementor ()
	{
		super ();
		if (getClass () == BaseOnChangeListenerImplementor.class)
			mono.android.TypeManager.Activate ("Google.Android.Material.Slider.IBaseOnChangeListenerImplementor, Xamarin.Google.Android.Material", "", this, new java.lang.Object[] {  });
	}


	public void onValueChange (java.lang.Object p0, float p1, boolean p2)
	{
		n_onValueChange (p0, p1, p2);
	}

	private native void n_onValueChange (java.lang.Object p0, float p1, boolean p2);

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
