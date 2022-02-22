package crc6477f0d89a9cfd64b1;


public class DragAndDropGestureHandler
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		android.view.View.OnDragListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onDrag:(Landroid/view/View;Landroid/view/DragEvent;)Z:GetOnDrag_Landroid_view_View_Landroid_view_DragEvent_Handler:Android.Views.View/IOnDragListenerInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Controls.Compatibility.Platform.Android.DragAndDropGestureHandler, Microsoft.Maui.Controls.Compatibility", DragAndDropGestureHandler.class, __md_methods);
	}


	public DragAndDropGestureHandler ()
	{
		super ();
		if (getClass () == DragAndDropGestureHandler.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.DragAndDropGestureHandler, Microsoft.Maui.Controls.Compatibility", "", this, new java.lang.Object[] {  });
	}


	public boolean onDrag (android.view.View p0, android.view.DragEvent p1)
	{
		return n_onDrag (p0, p1);
	}

	private native boolean n_onDrag (android.view.View p0, android.view.DragEvent p1);

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
