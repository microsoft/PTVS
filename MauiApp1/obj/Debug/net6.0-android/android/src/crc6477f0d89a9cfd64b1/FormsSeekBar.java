package crc6477f0d89a9cfd64b1;


public class FormsSeekBar
	extends android.widget.SeekBar
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onTouchEvent:(Landroid/view/MotionEvent;)Z:GetOnTouchEvent_Landroid_view_MotionEvent_Handler\n" +
			"n_isPressed:()Z:GetIsPressedHandler\n" +
			"n_setPressed:(Z)V:GetSetPressed_ZHandler\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Controls.Compatibility.Platform.Android.FormsSeekBar, Microsoft.Maui.Controls.Compatibility", FormsSeekBar.class, __md_methods);
	}


	public FormsSeekBar (android.content.Context p0)
	{
		super (p0);
		if (getClass () == FormsSeekBar.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.FormsSeekBar, Microsoft.Maui.Controls.Compatibility", "Android.Content.Context, Mono.Android", this, new java.lang.Object[] { p0 });
	}


	public FormsSeekBar (android.content.Context p0, android.util.AttributeSet p1)
	{
		super (p0, p1);
		if (getClass () == FormsSeekBar.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.FormsSeekBar, Microsoft.Maui.Controls.Compatibility", "Android.Content.Context, Mono.Android:Android.Util.IAttributeSet, Mono.Android", this, new java.lang.Object[] { p0, p1 });
	}


	public FormsSeekBar (android.content.Context p0, android.util.AttributeSet p1, int p2)
	{
		super (p0, p1, p2);
		if (getClass () == FormsSeekBar.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.FormsSeekBar, Microsoft.Maui.Controls.Compatibility", "Android.Content.Context, Mono.Android:Android.Util.IAttributeSet, Mono.Android:System.Int32, System.Private.CoreLib", this, new java.lang.Object[] { p0, p1, p2 });
	}


	public FormsSeekBar (android.content.Context p0, android.util.AttributeSet p1, int p2, int p3)
	{
		super (p0, p1, p2, p3);
		if (getClass () == FormsSeekBar.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.FormsSeekBar, Microsoft.Maui.Controls.Compatibility", "Android.Content.Context, Mono.Android:Android.Util.IAttributeSet, Mono.Android:System.Int32, System.Private.CoreLib:System.Int32, System.Private.CoreLib", this, new java.lang.Object[] { p0, p1, p2, p3 });
	}


	public boolean onTouchEvent (android.view.MotionEvent p0)
	{
		return n_onTouchEvent (p0);
	}

	private native boolean n_onTouchEvent (android.view.MotionEvent p0);


	public boolean isPressed ()
	{
		return n_isPressed ();
	}

	private native boolean n_isPressed ();


	public void setPressed (boolean p0)
	{
		n_setPressed (p0);
	}

	private native void n_setPressed (boolean p0);

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
