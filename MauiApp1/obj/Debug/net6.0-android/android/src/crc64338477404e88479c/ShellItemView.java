package crc64338477404e88479c;


public class ShellItemView
	extends crc64338477404e88479c.ShellItemViewBase
	implements
		mono.android.IGCUserPeer,
		com.google.android.material.navigation.NavigationBarView.OnItemSelectedListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onCreateView:(Landroid/view/LayoutInflater;Landroid/view/ViewGroup;Landroid/os/Bundle;)Landroid/view/View;:GetOnCreateView_Landroid_view_LayoutInflater_Landroid_view_ViewGroup_Landroid_os_Bundle_Handler\n" +
			"n_onDestroy:()V:GetOnDestroyHandler\n" +
			"n_onNavigationItemSelected:(Landroid/view/MenuItem;)Z:GetOnNavigationItemSelected_Landroid_view_MenuItem_Handler:Google.Android.Material.Navigation.NavigationBarView/IOnItemSelectedListenerInvoker, Xamarin.Google.Android.Material\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Controls.Platform.ShellItemView, Microsoft.Maui.Controls", ShellItemView.class, __md_methods);
	}


	public ShellItemView ()
	{
		super ();
		if (getClass () == ShellItemView.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Platform.ShellItemView, Microsoft.Maui.Controls", "", this, new java.lang.Object[] {  });
	}


	public ShellItemView (int p0)
	{
		super (p0);
		if (getClass () == ShellItemView.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Platform.ShellItemView, Microsoft.Maui.Controls", "System.Int32, System.Private.CoreLib", this, new java.lang.Object[] { p0 });
	}


	public android.view.View onCreateView (android.view.LayoutInflater p0, android.view.ViewGroup p1, android.os.Bundle p2)
	{
		return n_onCreateView (p0, p1, p2);
	}

	private native android.view.View n_onCreateView (android.view.LayoutInflater p0, android.view.ViewGroup p1, android.os.Bundle p2);


	public void onDestroy ()
	{
		n_onDestroy ();
	}

	private native void n_onDestroy ();


	public boolean onNavigationItemSelected (android.view.MenuItem p0)
	{
		return n_onNavigationItemSelected (p0);
	}

	private native boolean n_onNavigationItemSelected (android.view.MenuItem p0);

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
