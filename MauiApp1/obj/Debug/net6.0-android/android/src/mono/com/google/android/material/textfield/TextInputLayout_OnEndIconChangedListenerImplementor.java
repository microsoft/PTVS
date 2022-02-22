package mono.com.google.android.material.textfield;


public class TextInputLayout_OnEndIconChangedListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		com.google.android.material.textfield.TextInputLayout.OnEndIconChangedListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onEndIconChanged:(Lcom/google/android/material/textfield/TextInputLayout;I)V:GetOnEndIconChanged_Lcom_google_android_material_textfield_TextInputLayout_IHandler:Google.Android.Material.TextField.TextInputLayout/IOnEndIconChangedListenerInvoker, Xamarin.Google.Android.Material\n" +
			"";
		mono.android.Runtime.register ("Google.Android.Material.TextField.TextInputLayout+IOnEndIconChangedListenerImplementor, Xamarin.Google.Android.Material", TextInputLayout_OnEndIconChangedListenerImplementor.class, __md_methods);
	}


	public TextInputLayout_OnEndIconChangedListenerImplementor ()
	{
		super ();
		if (getClass () == TextInputLayout_OnEndIconChangedListenerImplementor.class)
			mono.android.TypeManager.Activate ("Google.Android.Material.TextField.TextInputLayout+IOnEndIconChangedListenerImplementor, Xamarin.Google.Android.Material", "", this, new java.lang.Object[] {  });
	}


	public void onEndIconChanged (com.google.android.material.textfield.TextInputLayout p0, int p1)
	{
		n_onEndIconChanged (p0, p1);
	}

	private native void n_onEndIconChanged (com.google.android.material.textfield.TextInputLayout p0, int p1);

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
