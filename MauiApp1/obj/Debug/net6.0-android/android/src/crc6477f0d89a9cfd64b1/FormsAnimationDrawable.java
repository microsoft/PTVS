package crc6477f0d89a9cfd64b1;


public class FormsAnimationDrawable
	extends android.graphics.drawable.AnimationDrawable
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_isRunning:()Z:GetIsRunningHandler\n" +
			"n_start:()V:GetStartHandler\n" +
			"n_stop:()V:GetStopHandler\n" +
			"n_selectDrawable:(I)Z:GetSelectDrawable_IHandler\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Controls.Compatibility.Platform.Android.FormsAnimationDrawable, Microsoft.Maui.Controls.Compatibility", FormsAnimationDrawable.class, __md_methods);
	}


	public FormsAnimationDrawable ()
	{
		super ();
		if (getClass () == FormsAnimationDrawable.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.FormsAnimationDrawable, Microsoft.Maui.Controls.Compatibility", "", this, new java.lang.Object[] {  });
	}


	public boolean isRunning ()
	{
		return n_isRunning ();
	}

	private native boolean n_isRunning ();


	public void start ()
	{
		n_start ();
	}

	private native void n_start ();


	public void stop ()
	{
		n_stop ();
	}

	private native void n_stop ();


	public boolean selectDrawable (int p0)
	{
		return n_selectDrawable (p0);
	}

	private native boolean n_selectDrawable (int p0);

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
