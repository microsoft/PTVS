package crc6477f0d89a9cfd64b1;


public class PositionalSmoothScroller
	extends androidx.recyclerview.widget.LinearSmoothScroller
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_getVerticalSnapPreference:()I:GetGetVerticalSnapPreferenceHandler\n" +
			"n_getHorizontalSnapPreference:()I:GetGetHorizontalSnapPreferenceHandler\n" +
			"n_calculateDtToFit:(IIIII)I:GetCalculateDtToFit_IIIIIHandler\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Controls.Compatibility.Platform.Android.PositionalSmoothScroller, Microsoft.Maui.Controls.Compatibility", PositionalSmoothScroller.class, __md_methods);
	}


	public PositionalSmoothScroller (android.content.Context p0)
	{
		super (p0);
		if (getClass () == PositionalSmoothScroller.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.PositionalSmoothScroller, Microsoft.Maui.Controls.Compatibility", "Android.Content.Context, Mono.Android", this, new java.lang.Object[] { p0 });
	}


	public int getVerticalSnapPreference ()
	{
		return n_getVerticalSnapPreference ();
	}

	private native int n_getVerticalSnapPreference ();


	public int getHorizontalSnapPreference ()
	{
		return n_getHorizontalSnapPreference ();
	}

	private native int n_getHorizontalSnapPreference ();


	public int calculateDtToFit (int p0, int p1, int p2, int p3, int p4)
	{
		return n_calculateDtToFit (p0, p1, p2, p3, p4);
	}

	private native int n_calculateDtToFit (int p0, int p1, int p2, int p3, int p4);

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
