package crc6452ffdc5b34af3a0f;


public class AccessibilityDelegateCompatWrapper
	extends androidx.core.view.AccessibilityDelegateCompat
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onInitializeAccessibilityNodeInfo:(Landroid/view/View;Landroidx/core/view/accessibility/AccessibilityNodeInfoCompat;)V:GetOnInitializeAccessibilityNodeInfo_Landroid_view_View_Landroidx_core_view_accessibility_AccessibilityNodeInfoCompat_Handler\n" +
			"n_sendAccessibilityEvent:(Landroid/view/View;I)V:GetSendAccessibilityEvent_Landroid_view_View_IHandler\n" +
			"n_sendAccessibilityEventUnchecked:(Landroid/view/View;Landroid/view/accessibility/AccessibilityEvent;)V:GetSendAccessibilityEventUnchecked_Landroid_view_View_Landroid_view_accessibility_AccessibilityEvent_Handler\n" +
			"n_dispatchPopulateAccessibilityEvent:(Landroid/view/View;Landroid/view/accessibility/AccessibilityEvent;)Z:GetDispatchPopulateAccessibilityEvent_Landroid_view_View_Landroid_view_accessibility_AccessibilityEvent_Handler\n" +
			"n_onPopulateAccessibilityEvent:(Landroid/view/View;Landroid/view/accessibility/AccessibilityEvent;)V:GetOnPopulateAccessibilityEvent_Landroid_view_View_Landroid_view_accessibility_AccessibilityEvent_Handler\n" +
			"n_onInitializeAccessibilityEvent:(Landroid/view/View;Landroid/view/accessibility/AccessibilityEvent;)V:GetOnInitializeAccessibilityEvent_Landroid_view_View_Landroid_view_accessibility_AccessibilityEvent_Handler\n" +
			"n_onRequestSendAccessibilityEvent:(Landroid/view/ViewGroup;Landroid/view/View;Landroid/view/accessibility/AccessibilityEvent;)Z:GetOnRequestSendAccessibilityEvent_Landroid_view_ViewGroup_Landroid_view_View_Landroid_view_accessibility_AccessibilityEvent_Handler\n" +
			"n_performAccessibilityAction:(Landroid/view/View;ILandroid/os/Bundle;)Z:GetPerformAccessibilityAction_Landroid_view_View_ILandroid_os_Bundle_Handler\n" +
			"n_getAccessibilityNodeProvider:(Landroid/view/View;)Landroidx/core/view/accessibility/AccessibilityNodeProviderCompat;:GetGetAccessibilityNodeProvider_Landroid_view_View_Handler\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Platform.AccessibilityDelegateCompatWrapper, Microsoft.Maui", AccessibilityDelegateCompatWrapper.class, __md_methods);
	}


	public AccessibilityDelegateCompatWrapper ()
	{
		super ();
		if (getClass () == AccessibilityDelegateCompatWrapper.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Platform.AccessibilityDelegateCompatWrapper, Microsoft.Maui", "", this, new java.lang.Object[] {  });
	}


	public AccessibilityDelegateCompatWrapper (android.view.View.AccessibilityDelegate p0)
	{
		super (p0);
		if (getClass () == AccessibilityDelegateCompatWrapper.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Platform.AccessibilityDelegateCompatWrapper, Microsoft.Maui", "Android.Views.View+AccessibilityDelegate, Mono.Android", this, new java.lang.Object[] { p0 });
	}

	public AccessibilityDelegateCompatWrapper (androidx.core.view.AccessibilityDelegateCompat p0)
	{
		super ();
		if (getClass () == AccessibilityDelegateCompatWrapper.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Platform.AccessibilityDelegateCompatWrapper, Microsoft.Maui", "AndroidX.Core.View.AccessibilityDelegateCompat, Xamarin.AndroidX.Core", this, new java.lang.Object[] { p0 });
	}


	public void onInitializeAccessibilityNodeInfo (android.view.View p0, androidx.core.view.accessibility.AccessibilityNodeInfoCompat p1)
	{
		n_onInitializeAccessibilityNodeInfo (p0, p1);
	}

	private native void n_onInitializeAccessibilityNodeInfo (android.view.View p0, androidx.core.view.accessibility.AccessibilityNodeInfoCompat p1);


	public void sendAccessibilityEvent (android.view.View p0, int p1)
	{
		n_sendAccessibilityEvent (p0, p1);
	}

	private native void n_sendAccessibilityEvent (android.view.View p0, int p1);


	public void sendAccessibilityEventUnchecked (android.view.View p0, android.view.accessibility.AccessibilityEvent p1)
	{
		n_sendAccessibilityEventUnchecked (p0, p1);
	}

	private native void n_sendAccessibilityEventUnchecked (android.view.View p0, android.view.accessibility.AccessibilityEvent p1);


	public boolean dispatchPopulateAccessibilityEvent (android.view.View p0, android.view.accessibility.AccessibilityEvent p1)
	{
		return n_dispatchPopulateAccessibilityEvent (p0, p1);
	}

	private native boolean n_dispatchPopulateAccessibilityEvent (android.view.View p0, android.view.accessibility.AccessibilityEvent p1);


	public void onPopulateAccessibilityEvent (android.view.View p0, android.view.accessibility.AccessibilityEvent p1)
	{
		n_onPopulateAccessibilityEvent (p0, p1);
	}

	private native void n_onPopulateAccessibilityEvent (android.view.View p0, android.view.accessibility.AccessibilityEvent p1);


	public void onInitializeAccessibilityEvent (android.view.View p0, android.view.accessibility.AccessibilityEvent p1)
	{
		n_onInitializeAccessibilityEvent (p0, p1);
	}

	private native void n_onInitializeAccessibilityEvent (android.view.View p0, android.view.accessibility.AccessibilityEvent p1);


	public boolean onRequestSendAccessibilityEvent (android.view.ViewGroup p0, android.view.View p1, android.view.accessibility.AccessibilityEvent p2)
	{
		return n_onRequestSendAccessibilityEvent (p0, p1, p2);
	}

	private native boolean n_onRequestSendAccessibilityEvent (android.view.ViewGroup p0, android.view.View p1, android.view.accessibility.AccessibilityEvent p2);


	public boolean performAccessibilityAction (android.view.View p0, int p1, android.os.Bundle p2)
	{
		return n_performAccessibilityAction (p0, p1, p2);
	}

	private native boolean n_performAccessibilityAction (android.view.View p0, int p1, android.os.Bundle p2);


	public androidx.core.view.accessibility.AccessibilityNodeProviderCompat getAccessibilityNodeProvider (android.view.View p0)
	{
		return n_getAccessibilityNodeProvider (p0);
	}

	private native androidx.core.view.accessibility.AccessibilityNodeProviderCompat n_getAccessibilityNodeProvider (android.view.View p0);

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
