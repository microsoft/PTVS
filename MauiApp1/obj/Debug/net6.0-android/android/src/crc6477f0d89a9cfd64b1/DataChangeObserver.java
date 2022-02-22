package crc6477f0d89a9cfd64b1;


public class DataChangeObserver
	extends androidx.recyclerview.widget.RecyclerView.AdapterDataObserver
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onChanged:()V:GetOnChangedHandler\n" +
			"n_onItemRangeInserted:(II)V:GetOnItemRangeInserted_IIHandler\n" +
			"n_onItemRangeChanged:(II)V:GetOnItemRangeChanged_IIHandler\n" +
			"n_onItemRangeChanged:(IILjava/lang/Object;)V:GetOnItemRangeChanged_IILjava_lang_Object_Handler\n" +
			"n_onItemRangeRemoved:(II)V:GetOnItemRangeRemoved_IIHandler\n" +
			"n_onItemRangeMoved:(III)V:GetOnItemRangeMoved_IIIHandler\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Controls.Compatibility.Platform.Android.DataChangeObserver, Microsoft.Maui.Controls.Compatibility", DataChangeObserver.class, __md_methods);
	}


	public DataChangeObserver ()
	{
		super ();
		if (getClass () == DataChangeObserver.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.DataChangeObserver, Microsoft.Maui.Controls.Compatibility", "", this, new java.lang.Object[] {  });
	}


	public void onChanged ()
	{
		n_onChanged ();
	}

	private native void n_onChanged ();


	public void onItemRangeInserted (int p0, int p1)
	{
		n_onItemRangeInserted (p0, p1);
	}

	private native void n_onItemRangeInserted (int p0, int p1);


	public void onItemRangeChanged (int p0, int p1)
	{
		n_onItemRangeChanged (p0, p1);
	}

	private native void n_onItemRangeChanged (int p0, int p1);


	public void onItemRangeChanged (int p0, int p1, java.lang.Object p2)
	{
		n_onItemRangeChanged (p0, p1, p2);
	}

	private native void n_onItemRangeChanged (int p0, int p1, java.lang.Object p2);


	public void onItemRangeRemoved (int p0, int p1)
	{
		n_onItemRangeRemoved (p0, p1);
	}

	private native void n_onItemRangeRemoved (int p0, int p1);


	public void onItemRangeMoved (int p0, int p1, int p2)
	{
		n_onItemRangeMoved (p0, p1, p2);
	}

	private native void n_onItemRangeMoved (int p0, int p1, int p2);

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
