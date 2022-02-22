package mono.com.google.android.material.card;


public class MaterialCardView_OnCheckedChangeListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		com.google.android.material.card.MaterialCardView.OnCheckedChangeListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onCheckedChanged:(Lcom/google/android/material/card/MaterialCardView;Z)V:GetOnCheckedChanged_Lcom_google_android_material_card_MaterialCardView_ZHandler:Google.Android.Material.Card.MaterialCardView/IOnCheckedChangeListenerInvoker, Xamarin.Google.Android.Material\n" +
			"";
		mono.android.Runtime.register ("Google.Android.Material.Card.MaterialCardView+IOnCheckedChangeListenerImplementor, Xamarin.Google.Android.Material", MaterialCardView_OnCheckedChangeListenerImplementor.class, __md_methods);
	}


	public MaterialCardView_OnCheckedChangeListenerImplementor ()
	{
		super ();
		if (getClass () == MaterialCardView_OnCheckedChangeListenerImplementor.class)
			mono.android.TypeManager.Activate ("Google.Android.Material.Card.MaterialCardView+IOnCheckedChangeListenerImplementor, Xamarin.Google.Android.Material", "", this, new java.lang.Object[] {  });
	}


	public void onCheckedChanged (com.google.android.material.card.MaterialCardView p0, boolean p1)
	{
		n_onCheckedChanged (p0, p1);
	}

	private native void n_onCheckedChanged (com.google.android.material.card.MaterialCardView p0, boolean p1);

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
