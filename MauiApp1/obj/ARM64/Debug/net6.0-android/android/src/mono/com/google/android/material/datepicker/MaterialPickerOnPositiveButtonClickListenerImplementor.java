package mono.com.google.android.material.datepicker;


public class MaterialPickerOnPositiveButtonClickListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		com.google.android.material.datepicker.MaterialPickerOnPositiveButtonClickListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onPositiveButtonClick:(Ljava/lang/Object;)V:GetOnPositiveButtonClick_Ljava_lang_Object_Handler:Google.Android.Material.DatePicker.IMaterialPickerOnPositiveButtonClickListenerInvoker, Xamarin.Google.Android.Material\n" +
			"";
		mono.android.Runtime.register ("Google.Android.Material.DatePicker.IMaterialPickerOnPositiveButtonClickListenerImplementor, Xamarin.Google.Android.Material", MaterialPickerOnPositiveButtonClickListenerImplementor.class, __md_methods);
	}


	public MaterialPickerOnPositiveButtonClickListenerImplementor ()
	{
		super ();
		if (getClass () == MaterialPickerOnPositiveButtonClickListenerImplementor.class)
			mono.android.TypeManager.Activate ("Google.Android.Material.DatePicker.IMaterialPickerOnPositiveButtonClickListenerImplementor, Xamarin.Google.Android.Material", "", this, new java.lang.Object[] {  });
	}


	public void onPositiveButtonClick (java.lang.Object p0)
	{
		n_onPositiveButtonClick (p0);
	}

	private native void n_onPositiveButtonClick (java.lang.Object p0);

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
