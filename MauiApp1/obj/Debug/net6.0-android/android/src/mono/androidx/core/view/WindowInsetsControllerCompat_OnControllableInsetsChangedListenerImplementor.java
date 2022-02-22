package mono.androidx.core.view;


public class WindowInsetsControllerCompat_OnControllableInsetsChangedListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		androidx.core.view.WindowInsetsControllerCompat.OnControllableInsetsChangedListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onControllableInsetsChanged:(Landroidx/core/view/WindowInsetsControllerCompat;I)V:GetOnControllableInsetsChanged_Landroidx_core_view_WindowInsetsControllerCompat_IHandler:AndroidX.Core.View.WindowInsetsControllerCompat/IOnControllableInsetsChangedListenerInvoker, Xamarin.AndroidX.Core\n" +
			"";
		mono.android.Runtime.register ("AndroidX.Core.View.WindowInsetsControllerCompat+IOnControllableInsetsChangedListenerImplementor, Xamarin.AndroidX.Core", WindowInsetsControllerCompat_OnControllableInsetsChangedListenerImplementor.class, __md_methods);
	}


	public WindowInsetsControllerCompat_OnControllableInsetsChangedListenerImplementor ()
	{
		super ();
		if (getClass () == WindowInsetsControllerCompat_OnControllableInsetsChangedListenerImplementor.class)
			mono.android.TypeManager.Activate ("AndroidX.Core.View.WindowInsetsControllerCompat+IOnControllableInsetsChangedListenerImplementor, Xamarin.AndroidX.Core", "", this, new java.lang.Object[] {  });
	}


	public void onControllableInsetsChanged (androidx.core.view.WindowInsetsControllerCompat p0, int p1)
	{
		n_onControllableInsetsChanged (p0, p1);
	}

	private native void n_onControllableInsetsChanged (androidx.core.view.WindowInsetsControllerCompat p0, int p1);

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
