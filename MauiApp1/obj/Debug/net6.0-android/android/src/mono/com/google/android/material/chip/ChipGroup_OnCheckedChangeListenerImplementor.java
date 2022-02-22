package mono.com.google.android.material.chip;


public class ChipGroup_OnCheckedChangeListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		com.google.android.material.chip.ChipGroup.OnCheckedChangeListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onCheckedChanged:(Lcom/google/android/material/chip/ChipGroup;I)V:GetOnCheckedChanged_Lcom_google_android_material_chip_ChipGroup_IHandler:Google.Android.Material.Chip.ChipGroup/IOnCheckedChangeListenerInvoker, Xamarin.Google.Android.Material\n" +
			"";
		mono.android.Runtime.register ("Google.Android.Material.Chip.ChipGroup+IOnCheckedChangeListenerImplementor, Xamarin.Google.Android.Material", ChipGroup_OnCheckedChangeListenerImplementor.class, __md_methods);
	}


	public ChipGroup_OnCheckedChangeListenerImplementor ()
	{
		super ();
		if (getClass () == ChipGroup_OnCheckedChangeListenerImplementor.class)
			mono.android.TypeManager.Activate ("Google.Android.Material.Chip.ChipGroup+IOnCheckedChangeListenerImplementor, Xamarin.Google.Android.Material", "", this, new java.lang.Object[] {  });
	}


	public void onCheckedChanged (com.google.android.material.chip.ChipGroup p0, int p1)
	{
		n_onCheckedChanged (p0, p1);
	}

	private native void n_onCheckedChanged (com.google.android.material.chip.ChipGroup p0, int p1);

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
