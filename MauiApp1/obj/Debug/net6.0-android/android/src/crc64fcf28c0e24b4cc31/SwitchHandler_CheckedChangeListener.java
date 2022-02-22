package crc64fcf28c0e24b4cc31;


public class SwitchHandler_CheckedChangeListener
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		android.widget.CompoundButton.OnCheckedChangeListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onCheckedChanged:(Landroid/widget/CompoundButton;Z)V:GetOnCheckedChanged_Landroid_widget_CompoundButton_ZHandler:Android.Widget.CompoundButton/IOnCheckedChangeListenerInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Handlers.SwitchHandler+CheckedChangeListener, Microsoft.Maui", SwitchHandler_CheckedChangeListener.class, __md_methods);
	}


	public SwitchHandler_CheckedChangeListener ()
	{
		super ();
		if (getClass () == SwitchHandler_CheckedChangeListener.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Handlers.SwitchHandler+CheckedChangeListener, Microsoft.Maui", "", this, new java.lang.Object[] {  });
	}


	public void onCheckedChanged (android.widget.CompoundButton p0, boolean p1)
	{
		n_onCheckedChanged (p0, p1);
	}

	private native void n_onCheckedChanged (android.widget.CompoundButton p0, boolean p1);

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
