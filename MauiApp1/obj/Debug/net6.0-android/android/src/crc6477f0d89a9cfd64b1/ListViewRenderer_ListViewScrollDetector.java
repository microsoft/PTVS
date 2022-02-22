package crc6477f0d89a9cfd64b1;


public class ListViewRenderer_ListViewScrollDetector
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		android.widget.AbsListView.OnScrollListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onScroll:(Landroid/widget/AbsListView;III)V:GetOnScroll_Landroid_widget_AbsListView_IIIHandler:Android.Widget.AbsListView/IOnScrollListenerInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"n_onScrollStateChanged:(Landroid/widget/AbsListView;I)V:GetOnScrollStateChanged_Landroid_widget_AbsListView_IHandler:Android.Widget.AbsListView/IOnScrollListenerInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Controls.Compatibility.Platform.Android.ListViewRenderer+ListViewScrollDetector, Microsoft.Maui.Controls.Compatibility", ListViewRenderer_ListViewScrollDetector.class, __md_methods);
	}


	public ListViewRenderer_ListViewScrollDetector ()
	{
		super ();
		if (getClass () == ListViewRenderer_ListViewScrollDetector.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.ListViewRenderer+ListViewScrollDetector, Microsoft.Maui.Controls.Compatibility", "", this, new java.lang.Object[] {  });
	}

	public ListViewRenderer_ListViewScrollDetector (crc6477f0d89a9cfd64b1.ListViewRenderer p0)
	{
		super ();
		if (getClass () == ListViewRenderer_ListViewScrollDetector.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.ListViewRenderer+ListViewScrollDetector, Microsoft.Maui.Controls.Compatibility", "Microsoft.Maui.Controls.Compatibility.Platform.Android.ListViewRenderer, Microsoft.Maui.Controls.Compatibility", this, new java.lang.Object[] { p0 });
	}


	public void onScroll (android.widget.AbsListView p0, int p1, int p2, int p3)
	{
		n_onScroll (p0, p1, p2, p3);
	}

	private native void n_onScroll (android.widget.AbsListView p0, int p1, int p2, int p3);


	public void onScrollStateChanged (android.widget.AbsListView p0, int p1)
	{
		n_onScrollStateChanged (p0, p1);
	}

	private native void n_onScrollStateChanged (android.widget.AbsListView p0, int p1);

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
