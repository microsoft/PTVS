package crc6452ffdc5b34af3a0f;


public class StepperHandlerHolder
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Platform.StepperHandlerHolder, Microsoft.Maui", StepperHandlerHolder.class, __md_methods);
	}


	public StepperHandlerHolder ()
	{
		super ();
		if (getClass () == StepperHandlerHolder.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Platform.StepperHandlerHolder, Microsoft.Maui", "", this, new java.lang.Object[] {  });
	}

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
