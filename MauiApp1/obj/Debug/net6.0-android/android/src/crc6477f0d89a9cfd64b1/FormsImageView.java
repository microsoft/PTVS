package crc6477f0d89a9cfd64b1;


public class FormsImageView
	extends android.widget.ImageView
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_invalidate:()V:GetInvalidateHandler\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Controls.Compatibility.Platform.Android.FormsImageView, Microsoft.Maui.Controls.Compatibility", FormsImageView.class, __md_methods);
	}


	public FormsImageView (android.content.Context p0)
	{
		super (p0);
		if (getClass () == FormsImageView.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.FormsImageView, Microsoft.Maui.Controls.Compatibility", "Android.Content.Context, Mono.Android", this, new java.lang.Object[] { p0 });
	}


	public FormsImageView (android.content.Context p0, android.util.AttributeSet p1)
	{
		super (p0, p1);
		if (getClass () == FormsImageView.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.FormsImageView, Microsoft.Maui.Controls.Compatibility", "Android.Content.Context, Mono.Android:Android.Util.IAttributeSet, Mono.Android", this, new java.lang.Object[] { p0, p1 });
	}


	public FormsImageView (android.content.Context p0, android.util.AttributeSet p1, int p2)
	{
		super (p0, p1, p2);
		if (getClass () == FormsImageView.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.FormsImageView, Microsoft.Maui.Controls.Compatibility", "Android.Content.Context, Mono.Android:Android.Util.IAttributeSet, Mono.Android:System.Int32, System.Private.CoreLib", this, new java.lang.Object[] { p0, p1, p2 });
	}


	public FormsImageView (android.content.Context p0, android.util.AttributeSet p1, int p2, int p3)
	{
		super (p0, p1, p2, p3);
		if (getClass () == FormsImageView.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.FormsImageView, Microsoft.Maui.Controls.Compatibility", "Android.Content.Context, Mono.Android:Android.Util.IAttributeSet, Mono.Android:System.Int32, System.Private.CoreLib:System.Int32, System.Private.CoreLib", this, new java.lang.Object[] { p0, p1, p2, p3 });
	}


	public void invalidate ()
	{
		n_invalidate ();
	}

	private native void n_invalidate ();

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
