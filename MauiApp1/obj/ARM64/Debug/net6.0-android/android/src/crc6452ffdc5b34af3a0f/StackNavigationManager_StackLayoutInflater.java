package crc6452ffdc5b34af3a0f;


public class StackNavigationManager_StackLayoutInflater
	extends android.view.LayoutInflater
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_cloneInContext:(Landroid/content/Context;)Landroid/view/LayoutInflater;:GetCloneInContext_Landroid_content_Context_Handler\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Platform.StackNavigationManager+StackLayoutInflater, Microsoft.Maui", StackNavigationManager_StackLayoutInflater.class, __md_methods);
	}


	public StackNavigationManager_StackLayoutInflater (android.content.Context p0)
	{
		super (p0);
		if (getClass () == StackNavigationManager_StackLayoutInflater.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Platform.StackNavigationManager+StackLayoutInflater, Microsoft.Maui", "Android.Content.Context, Mono.Android", this, new java.lang.Object[] { p0 });
	}


	public StackNavigationManager_StackLayoutInflater (android.view.LayoutInflater p0, android.content.Context p1)
	{
		super (p0, p1);
		if (getClass () == StackNavigationManager_StackLayoutInflater.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Platform.StackNavigationManager+StackLayoutInflater, Microsoft.Maui", "Android.Views.LayoutInflater, Mono.Android:Android.Content.Context, Mono.Android", this, new java.lang.Object[] { p0, p1 });
	}


	public android.view.LayoutInflater cloneInContext (android.content.Context p0)
	{
		return n_cloneInContext (p0);
	}

	private native android.view.LayoutInflater n_cloneInContext (android.content.Context p0);

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
