package mono.com.google.android.material.button;


public class MaterialButton_OnCheckedChangeListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		com.google.android.material.button.MaterialButton.OnCheckedChangeListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onCheckedChanged:(Lcom/google/android/material/button/MaterialButton;Z)V:GetOnCheckedChanged_Lcom_google_android_material_button_MaterialButton_ZHandler:Google.Android.Material.Button.MaterialButton/IOnCheckedChangeListenerInvoker, Xamarin.Google.Android.Material\n" +
			"";
		mono.android.Runtime.register ("Google.Android.Material.Button.MaterialButton+IOnCheckedChangeListenerImplementor, Xamarin.Google.Android.Material", MaterialButton_OnCheckedChangeListenerImplementor.class, __md_methods);
	}


	public MaterialButton_OnCheckedChangeListenerImplementor ()
	{
		super ();
		if (getClass () == MaterialButton_OnCheckedChangeListenerImplementor.class)
			mono.android.TypeManager.Activate ("Google.Android.Material.Button.MaterialButton+IOnCheckedChangeListenerImplementor, Xamarin.Google.Android.Material", "", this, new java.lang.Object[] {  });
	}


	public void onCheckedChanged (com.google.android.material.button.MaterialButton p0, boolean p1)
	{
		n_onCheckedChanged (p0, p1);
	}

	private native void n_onCheckedChanged (com.google.android.material.button.MaterialButton p0, boolean p1);

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
