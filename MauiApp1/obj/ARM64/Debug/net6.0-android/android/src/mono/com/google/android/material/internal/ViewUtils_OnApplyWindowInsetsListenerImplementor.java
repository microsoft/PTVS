package mono.com.google.android.material.internal;


public class ViewUtils_OnApplyWindowInsetsListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		com.google.android.material.internal.ViewUtils.OnApplyWindowInsetsListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onApplyWindowInsets:(Landroid/view/View;Landroidx/core/view/WindowInsetsCompat;Lcom/google/android/material/internal/ViewUtils$RelativePadding;)Landroidx/core/view/WindowInsetsCompat;:GetOnApplyWindowInsets_Landroid_view_View_Landroidx_core_view_WindowInsetsCompat_Lcom_google_android_material_internal_ViewUtils_RelativePadding_Handler:Google.Android.Material.Internal.ViewUtils/IOnApplyWindowInsetsListenerInvoker, Xamarin.Google.Android.Material\n" +
			"";
		mono.android.Runtime.register ("Google.Android.Material.Internal.ViewUtils+IOnApplyWindowInsetsListenerImplementor, Xamarin.Google.Android.Material", ViewUtils_OnApplyWindowInsetsListenerImplementor.class, __md_methods);
	}


	public ViewUtils_OnApplyWindowInsetsListenerImplementor ()
	{
		super ();
		if (getClass () == ViewUtils_OnApplyWindowInsetsListenerImplementor.class)
			mono.android.TypeManager.Activate ("Google.Android.Material.Internal.ViewUtils+IOnApplyWindowInsetsListenerImplementor, Xamarin.Google.Android.Material", "", this, new java.lang.Object[] {  });
	}


	public androidx.core.view.WindowInsetsCompat onApplyWindowInsets (android.view.View p0, androidx.core.view.WindowInsetsCompat p1, com.google.android.material.internal.ViewUtils.RelativePadding p2)
	{
		return n_onApplyWindowInsets (p0, p1, p2);
	}

	private native androidx.core.view.WindowInsetsCompat n_onApplyWindowInsets (android.view.View p0, androidx.core.view.WindowInsetsCompat p1, com.google.android.material.internal.ViewUtils.RelativePadding p2);

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
