package crc6477f0d89a9cfd64b1;


public class ShellFlyoutRenderer
	extends androidx.drawerlayout.widget.DrawerLayout
	implements
		mono.android.IGCUserPeer,
		androidx.drawerlayout.widget.DrawerLayout.DrawerListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onInterceptTouchEvent:(Landroid/view/MotionEvent;)Z:GetOnInterceptTouchEvent_Landroid_view_MotionEvent_Handler\n" +
			"n_drawChild:(Landroid/graphics/Canvas;Landroid/view/View;J)Z:GetDrawChild_Landroid_graphics_Canvas_Landroid_view_View_JHandler\n" +
			"n_onDrawerClosed:(Landroid/view/View;)V:GetOnDrawerClosed_Landroid_view_View_Handler:AndroidX.DrawerLayout.Widget.DrawerLayout/IDrawerListenerInvoker, Xamarin.AndroidX.DrawerLayout\n" +
			"n_onDrawerOpened:(Landroid/view/View;)V:GetOnDrawerOpened_Landroid_view_View_Handler:AndroidX.DrawerLayout.Widget.DrawerLayout/IDrawerListenerInvoker, Xamarin.AndroidX.DrawerLayout\n" +
			"n_onDrawerSlide:(Landroid/view/View;F)V:GetOnDrawerSlide_Landroid_view_View_FHandler:AndroidX.DrawerLayout.Widget.DrawerLayout/IDrawerListenerInvoker, Xamarin.AndroidX.DrawerLayout\n" +
			"n_onDrawerStateChanged:(I)V:GetOnDrawerStateChanged_IHandler:AndroidX.DrawerLayout.Widget.DrawerLayout/IDrawerListenerInvoker, Xamarin.AndroidX.DrawerLayout\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Controls.Compatibility.Platform.Android.ShellFlyoutRenderer, Microsoft.Maui.Controls.Compatibility", ShellFlyoutRenderer.class, __md_methods);
	}


	public ShellFlyoutRenderer (android.content.Context p0)
	{
		super (p0);
		if (getClass () == ShellFlyoutRenderer.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.ShellFlyoutRenderer, Microsoft.Maui.Controls.Compatibility", "Android.Content.Context, Mono.Android", this, new java.lang.Object[] { p0 });
	}


	public ShellFlyoutRenderer (android.content.Context p0, android.util.AttributeSet p1)
	{
		super (p0, p1);
		if (getClass () == ShellFlyoutRenderer.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.ShellFlyoutRenderer, Microsoft.Maui.Controls.Compatibility", "Android.Content.Context, Mono.Android:Android.Util.IAttributeSet, Mono.Android", this, new java.lang.Object[] { p0, p1 });
	}


	public ShellFlyoutRenderer (android.content.Context p0, android.util.AttributeSet p1, int p2)
	{
		super (p0, p1, p2);
		if (getClass () == ShellFlyoutRenderer.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.ShellFlyoutRenderer, Microsoft.Maui.Controls.Compatibility", "Android.Content.Context, Mono.Android:Android.Util.IAttributeSet, Mono.Android:System.Int32, System.Private.CoreLib", this, new java.lang.Object[] { p0, p1, p2 });
	}


	public boolean onInterceptTouchEvent (android.view.MotionEvent p0)
	{
		return n_onInterceptTouchEvent (p0);
	}

	private native boolean n_onInterceptTouchEvent (android.view.MotionEvent p0);


	public boolean drawChild (android.graphics.Canvas p0, android.view.View p1, long p2)
	{
		return n_drawChild (p0, p1, p2);
	}

	private native boolean n_drawChild (android.graphics.Canvas p0, android.view.View p1, long p2);


	public void onDrawerClosed (android.view.View p0)
	{
		n_onDrawerClosed (p0);
	}

	private native void n_onDrawerClosed (android.view.View p0);


	public void onDrawerOpened (android.view.View p0)
	{
		n_onDrawerOpened (p0);
	}

	private native void n_onDrawerOpened (android.view.View p0);


	public void onDrawerSlide (android.view.View p0, float p1)
	{
		n_onDrawerSlide (p0, p1);
	}

	private native void n_onDrawerSlide (android.view.View p0, float p1);


	public void onDrawerStateChanged (int p0)
	{
		n_onDrawerStateChanged (p0);
	}

	private native void n_onDrawerStateChanged (int p0);

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
