package crc6477f0d89a9cfd64b1;


public class FormsEditText
	extends crc6477f0d89a9cfd64b1.FormsEditTextBase
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onKeyPreIme:(ILandroid/view/KeyEvent;)Z:GetOnKeyPreIme_ILandroid_view_KeyEvent_Handler\n" +
			"n_onSelectionChanged:(II)V:GetOnSelectionChanged_IIHandler\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Controls.Compatibility.Platform.Android.FormsEditText, Microsoft.Maui.Controls.Compatibility", FormsEditText.class, __md_methods);
	}


	public FormsEditText (android.content.Context p0)
	{
		super (p0);
		if (getClass () == FormsEditText.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.FormsEditText, Microsoft.Maui.Controls.Compatibility", "Android.Content.Context, Mono.Android", this, new java.lang.Object[] { p0 });
	}


	public FormsEditText (android.content.Context p0, android.util.AttributeSet p1)
	{
		super (p0, p1);
		if (getClass () == FormsEditText.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.FormsEditText, Microsoft.Maui.Controls.Compatibility", "Android.Content.Context, Mono.Android:Android.Util.IAttributeSet, Mono.Android", this, new java.lang.Object[] { p0, p1 });
	}


	public FormsEditText (android.content.Context p0, android.util.AttributeSet p1, int p2)
	{
		super (p0, p1, p2);
		if (getClass () == FormsEditText.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.FormsEditText, Microsoft.Maui.Controls.Compatibility", "Android.Content.Context, Mono.Android:Android.Util.IAttributeSet, Mono.Android:System.Int32, System.Private.CoreLib", this, new java.lang.Object[] { p0, p1, p2 });
	}


	public FormsEditText (android.content.Context p0, android.util.AttributeSet p1, int p2, int p3)
	{
		super (p0, p1, p2, p3);
		if (getClass () == FormsEditText.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.FormsEditText, Microsoft.Maui.Controls.Compatibility", "Android.Content.Context, Mono.Android:Android.Util.IAttributeSet, Mono.Android:System.Int32, System.Private.CoreLib:System.Int32, System.Private.CoreLib", this, new java.lang.Object[] { p0, p1, p2, p3 });
	}


	public boolean onKeyPreIme (int p0, android.view.KeyEvent p1)
	{
		return n_onKeyPreIme (p0, p1);
	}

	private native boolean n_onKeyPreIme (int p0, android.view.KeyEvent p1);


	public void onSelectionChanged (int p0, int p1)
	{
		n_onSelectionChanged (p0, p1);
	}

	private native void n_onSelectionChanged (int p0, int p1);

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
