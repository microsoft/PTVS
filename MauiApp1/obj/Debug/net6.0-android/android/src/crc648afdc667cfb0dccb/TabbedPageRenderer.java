package crc648afdc667cfb0dccb;


public class TabbedPageRenderer
	extends crc6477f0d89a9cfd64b1.VisualElementRenderer_1
	implements
		mono.android.IGCUserPeer,
		com.google.android.material.tabs.TabLayout.BaseOnTabSelectedListener,
		androidx.viewpager.widget.ViewPager.OnPageChangeListener,
		com.google.android.material.navigation.NavigationBarView.OnItemSelectedListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onAttachedToWindow:()V:GetOnAttachedToWindowHandler\n" +
			"n_onDetachedFromWindow:()V:GetOnDetachedFromWindowHandler\n" +
			"n_onLayout:(ZIIII)V:GetOnLayout_ZIIIIHandler\n" +
			"n_onTabReselected:(Lcom/google/android/material/tabs/TabLayout$Tab;)V:GetOnTabReselected_Lcom_google_android_material_tabs_TabLayout_Tab_Handler:Google.Android.Material.Tabs.TabLayout/IOnTabSelectedListenerInvoker, Xamarin.Google.Android.Material\n" +
			"n_onTabSelected:(Lcom/google/android/material/tabs/TabLayout$Tab;)V:GetOnTabSelected_Lcom_google_android_material_tabs_TabLayout_Tab_Handler:Google.Android.Material.Tabs.TabLayout/IOnTabSelectedListenerInvoker, Xamarin.Google.Android.Material\n" +
			"n_onTabUnselected:(Lcom/google/android/material/tabs/TabLayout$Tab;)V:GetOnTabUnselected_Lcom_google_android_material_tabs_TabLayout_Tab_Handler:Google.Android.Material.Tabs.TabLayout/IOnTabSelectedListenerInvoker, Xamarin.Google.Android.Material\n" +
			"n_onPageScrollStateChanged:(I)V:GetOnPageScrollStateChanged_IHandler:AndroidX.ViewPager.Widget.ViewPager/IOnPageChangeListenerInvoker, Xamarin.AndroidX.ViewPager\n" +
			"n_onPageScrolled:(IFI)V:GetOnPageScrolled_IFIHandler:AndroidX.ViewPager.Widget.ViewPager/IOnPageChangeListenerInvoker, Xamarin.AndroidX.ViewPager\n" +
			"n_onPageSelected:(I)V:GetOnPageSelected_IHandler:AndroidX.ViewPager.Widget.ViewPager/IOnPageChangeListenerInvoker, Xamarin.AndroidX.ViewPager\n" +
			"n_onNavigationItemSelected:(Landroid/view/MenuItem;)Z:GetOnNavigationItemSelected_Landroid_view_MenuItem_Handler:Google.Android.Material.Navigation.NavigationBarView/IOnItemSelectedListenerInvoker, Xamarin.Google.Android.Material\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Controls.Compatibility.Platform.Android.AppCompat.TabbedPageRenderer, Microsoft.Maui.Controls.Compatibility", TabbedPageRenderer.class, __md_methods);
	}


	public TabbedPageRenderer (android.content.Context p0)
	{
		super (p0);
		if (getClass () == TabbedPageRenderer.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.AppCompat.TabbedPageRenderer, Microsoft.Maui.Controls.Compatibility", "Android.Content.Context, Mono.Android", this, new java.lang.Object[] { p0 });
	}


	public TabbedPageRenderer (android.content.Context p0, android.util.AttributeSet p1)
	{
		super (p0, p1);
		if (getClass () == TabbedPageRenderer.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.AppCompat.TabbedPageRenderer, Microsoft.Maui.Controls.Compatibility", "Android.Content.Context, Mono.Android:Android.Util.IAttributeSet, Mono.Android", this, new java.lang.Object[] { p0, p1 });
	}


	public TabbedPageRenderer (android.content.Context p0, android.util.AttributeSet p1, int p2)
	{
		super (p0, p1, p2);
		if (getClass () == TabbedPageRenderer.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.AppCompat.TabbedPageRenderer, Microsoft.Maui.Controls.Compatibility", "Android.Content.Context, Mono.Android:Android.Util.IAttributeSet, Mono.Android:System.Int32, System.Private.CoreLib", this, new java.lang.Object[] { p0, p1, p2 });
	}


	public void onAttachedToWindow ()
	{
		n_onAttachedToWindow ();
	}

	private native void n_onAttachedToWindow ();


	public void onDetachedFromWindow ()
	{
		n_onDetachedFromWindow ();
	}

	private native void n_onDetachedFromWindow ();


	public void onLayout (boolean p0, int p1, int p2, int p3, int p4)
	{
		n_onLayout (p0, p1, p2, p3, p4);
	}

	private native void n_onLayout (boolean p0, int p1, int p2, int p3, int p4);


	public void onTabReselected (com.google.android.material.tabs.TabLayout.Tab p0)
	{
		n_onTabReselected (p0);
	}

	private native void n_onTabReselected (com.google.android.material.tabs.TabLayout.Tab p0);


	public void onTabSelected (com.google.android.material.tabs.TabLayout.Tab p0)
	{
		n_onTabSelected (p0);
	}

	private native void n_onTabSelected (com.google.android.material.tabs.TabLayout.Tab p0);


	public void onTabUnselected (com.google.android.material.tabs.TabLayout.Tab p0)
	{
		n_onTabUnselected (p0);
	}

	private native void n_onTabUnselected (com.google.android.material.tabs.TabLayout.Tab p0);


	public void onPageScrollStateChanged (int p0)
	{
		n_onPageScrollStateChanged (p0);
	}

	private native void n_onPageScrollStateChanged (int p0);


	public void onPageScrolled (int p0, float p1, int p2)
	{
		n_onPageScrolled (p0, p1, p2);
	}

	private native void n_onPageScrolled (int p0, float p1, int p2);


	public void onPageSelected (int p0)
	{
		n_onPageSelected (p0);
	}

	private native void n_onPageSelected (int p0);


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
