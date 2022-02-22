package mono.com.google.android.material.behavior;


public class SwipeDismissBehavior_OnDismissListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		com.google.android.material.behavior.SwipeDismissBehavior.OnDismissListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onDismiss:(Landroid/view/View;)V:GetOnDismiss_Landroid_view_View_Handler:Google.Android.Material.Behavior.SwipeDismissBehavior/IOnDismissListenerInvoker, Xamarin.Google.Android.Material\n" +
			"n_onDragStateChanged:(I)V:GetOnDragStateChanged_IHandler:Google.Android.Material.Behavior.SwipeDismissBehavior/IOnDismissListenerInvoker, Xamarin.Google.Android.Material\n" +
			"";
		mono.android.Runtime.register ("Google.Android.Material.Behavior.SwipeDismissBehavior+IOnDismissListenerImplementor, Xamarin.Google.Android.Material", SwipeDismissBehavior_OnDismissListenerImplementor.class, __md_methods);
	}


	public SwipeDismissBehavior_OnDismissListenerImplementor ()
	{
		super ();
		if (getClass () == SwipeDismissBehavior_OnDismissListenerImplementor.class)
			mono.android.TypeManager.Activate ("Google.Android.Material.Behavior.SwipeDismissBehavior+IOnDismissListenerImplementor, Xamarin.Google.Android.Material", "", this, new java.lang.Object[] {  });
	}


	public void onDismiss (android.view.View p0)
	{
		n_onDismiss (p0);
	}

	private native void n_onDismiss (android.view.View p0);


	public void onDragStateChanged (int p0)
	{
		n_onDragStateChanged (p0);
	}

	private native void n_onDragStateChanged (int p0);

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
