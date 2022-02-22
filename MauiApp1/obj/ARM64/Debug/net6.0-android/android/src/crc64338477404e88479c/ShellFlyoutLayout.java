package crc64338477404e88479c;


public class ShellFlyoutLayout
	extends androidx.coordinatorlayout.widget.CoordinatorLayout
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onLayout:(ZIIII)V:GetOnLayout_ZIIIIHandler\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Controls.Platform.ShellFlyoutLayout, Microsoft.Maui.Controls", ShellFlyoutLayout.class, __md_methods);
	}


	public ShellFlyoutLayout (android.content.Context p0)
	{
		super (p0);
		if (getClass () == ShellFlyoutLayout.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Platform.ShellFlyoutLayout, Microsoft.Maui.Controls", "Android.Content.Context, Mono.Android", this, new java.lang.Object[] { p0 });
	}


	public ShellFlyoutLayout (android.content.Context p0, android.util.AttributeSet p1)
	{
		super (p0, p1);
		if (getClass () == ShellFlyoutLayout.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Platform.ShellFlyoutLayout, Microsoft.Maui.Controls", "Android.Content.Context, Mono.Android:Android.Util.IAttributeSet, Mono.Android", this, new java.lang.Object[] { p0, p1 });
	}


	public ShellFlyoutLayout (android.content.Context p0, android.util.AttributeSet p1, int p2)
	{
		super (p0, p1, p2);
		if (getClass () == ShellFlyoutLayout.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Platform.ShellFlyoutLayout, Microsoft.Maui.Controls", "Android.Content.Context, Mono.Android:Android.Util.IAttributeSet, Mono.Android:System.Int32, System.Private.CoreLib", this, new java.lang.Object[] { p0, p1, p2 });
	}


	public void onLayout (boolean p0, int p1, int p2, int p3, int p4)
	{
		n_onLayout (p0, p1, p2, p3, p4);
	}

	private native void n_onLayout (boolean p0, int p1, int p2, int p3, int p4);

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
