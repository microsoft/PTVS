package androidx.appcompat.app;


public class AlertDialog_IDialogInterfaceOnCancelListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		android.content.DialogInterface.OnCancelListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onCancel:(Landroid/content/DialogInterface;)V:GetOnCancel_Landroid_content_DialogInterface_Handler:Android.Content.IDialogInterfaceOnCancelListenerInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"";
		mono.android.Runtime.register ("AndroidX.AppCompat.App.AlertDialog+IDialogInterfaceOnCancelListenerImplementor, Xamarin.AndroidX.AppCompat", AlertDialog_IDialogInterfaceOnCancelListenerImplementor.class, __md_methods);
	}


	public AlertDialog_IDialogInterfaceOnCancelListenerImplementor ()
	{
		super ();
		if (getClass () == AlertDialog_IDialogInterfaceOnCancelListenerImplementor.class)
			mono.android.TypeManager.Activate ("AndroidX.AppCompat.App.AlertDialog+IDialogInterfaceOnCancelListenerImplementor, Xamarin.AndroidX.AppCompat", "", this, new java.lang.Object[] {  });
	}


	public void onCancel (android.content.DialogInterface p0)
	{
		n_onCancel (p0);
	}

	private native void n_onCancel (android.content.DialogInterface p0);

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
