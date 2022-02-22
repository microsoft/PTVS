package crc6477f0d89a9cfd64b1;


public class ShellSearchViewAdapter_CustomFilter
	extends android.widget.Filter
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_performFiltering:(Ljava/lang/CharSequence;)Landroid/widget/Filter$FilterResults;:GetPerformFiltering_Ljava_lang_CharSequence_Handler\n" +
			"n_publishResults:(Ljava/lang/CharSequence;Landroid/widget/Filter$FilterResults;)V:GetPublishResults_Ljava_lang_CharSequence_Landroid_widget_Filter_FilterResults_Handler\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Controls.Compatibility.Platform.Android.ShellSearchViewAdapter+CustomFilter, Microsoft.Maui.Controls.Compatibility", ShellSearchViewAdapter_CustomFilter.class, __md_methods);
	}


	public ShellSearchViewAdapter_CustomFilter ()
	{
		super ();
		if (getClass () == ShellSearchViewAdapter_CustomFilter.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.ShellSearchViewAdapter+CustomFilter, Microsoft.Maui.Controls.Compatibility", "", this, new java.lang.Object[] {  });
	}

	public ShellSearchViewAdapter_CustomFilter (android.widget.BaseAdapter p0)
	{
		super ();
		if (getClass () == ShellSearchViewAdapter_CustomFilter.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.ShellSearchViewAdapter+CustomFilter, Microsoft.Maui.Controls.Compatibility", "Android.Widget.BaseAdapter, Mono.Android", this, new java.lang.Object[] { p0 });
	}


	public android.widget.Filter.FilterResults performFiltering (java.lang.CharSequence p0)
	{
		return n_performFiltering (p0);
	}

	private native android.widget.Filter.FilterResults n_performFiltering (java.lang.CharSequence p0);


	public void publishResults (java.lang.CharSequence p0, android.widget.Filter.FilterResults p1)
	{
		n_publishResults (p0, p1);
	}

	private native void n_publishResults (java.lang.CharSequence p0, android.widget.Filter.FilterResults p1);

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
