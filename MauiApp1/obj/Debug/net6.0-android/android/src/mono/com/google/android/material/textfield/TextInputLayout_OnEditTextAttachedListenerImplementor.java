package mono.com.google.android.material.textfield;


public class TextInputLayout_OnEditTextAttachedListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		com.google.android.material.textfield.TextInputLayout.OnEditTextAttachedListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onEditTextAttached:(Lcom/google/android/material/textfield/TextInputLayout;)V:GetOnEditTextAttached_Lcom_google_android_material_textfield_TextInputLayout_Handler:Google.Android.Material.TextField.TextInputLayout/IOnEditTextAttachedListenerInvoker, Xamarin.Google.Android.Material\n" +
			"";
		mono.android.Runtime.register ("Google.Android.Material.TextField.TextInputLayout+IOnEditTextAttachedListenerImplementor, Xamarin.Google.Android.Material", TextInputLayout_OnEditTextAttachedListenerImplementor.class, __md_methods);
	}


	public TextInputLayout_OnEditTextAttachedListenerImplementor ()
	{
		super ();
		if (getClass () == TextInputLayout_OnEditTextAttachedListenerImplementor.class)
			mono.android.TypeManager.Activate ("Google.Android.Material.TextField.TextInputLayout+IOnEditTextAttachedListenerImplementor, Xamarin.Google.Android.Material", "", this, new java.lang.Object[] {  });
	}


	public void onEditTextAttached (com.google.android.material.textfield.TextInputLayout p0)
	{
		n_onEditTextAttached (p0);
	}

	private native void n_onEditTextAttached (com.google.android.material.textfield.TextInputLayout p0);

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
