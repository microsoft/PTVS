package mono.androidx.appcompat.widget;


public class ContentFrameLayout_OnAttachListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		androidx.appcompat.widget.ContentFrameLayout.OnAttachListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onAttachedFromWindow:()V:GetOnAttachedFromWindowHandler:AndroidX.AppCompat.Widget.ContentFrameLayout/IOnAttachListenerInvoker, Xamarin.AndroidX.AppCompat\n" +
			"n_onDetachedFromWindow:()V:GetOnDetachedFromWindowHandler:AndroidX.AppCompat.Widget.ContentFrameLayout/IOnAttachListenerInvoker, Xamarin.AndroidX.AppCompat\n" +
			"";
		mono.android.Runtime.register ("AndroidX.AppCompat.Widget.ContentFrameLayout+IOnAttachListenerImplementor, Xamarin.AndroidX.AppCompat", ContentFrameLayout_OnAttachListenerImplementor.class, __md_methods);
	}


	public ContentFrameLayout_OnAttachListenerImplementor ()
	{
		super ();
		if (getClass () == ContentFrameLayout_OnAttachListenerImplementor.class)
			mono.android.TypeManager.Activate ("AndroidX.AppCompat.Widget.ContentFrameLayout+IOnAttachListenerImplementor, Xamarin.AndroidX.AppCompat", "", this, new java.lang.Object[] {  });
	}


	public void onAttachedFromWindow ()
	{
		n_onAttachedFromWindow ();
	}

	private native void n_onAttachedFromWindow ();


	public void onDetachedFromWindow ()
	{
		n_onDetachedFromWindow ();
	}

	private native void n_onDetachedFromWindow ();

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
