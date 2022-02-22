package crc6477f0d89a9cfd64b1;


public class CarouselViewRenderer_CarouselViewwOnGlobalLayoutListener
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		android.view.ViewTreeObserver.OnGlobalLayoutListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onGlobalLayout:()V:GetOnGlobalLayoutHandler:Android.Views.ViewTreeObserver/IOnGlobalLayoutListenerInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Controls.Compatibility.Platform.Android.CarouselViewRenderer+CarouselViewwOnGlobalLayoutListener, Microsoft.Maui.Controls.Compatibility", CarouselViewRenderer_CarouselViewwOnGlobalLayoutListener.class, __md_methods);
	}


	public CarouselViewRenderer_CarouselViewwOnGlobalLayoutListener ()
	{
		super ();
		if (getClass () == CarouselViewRenderer_CarouselViewwOnGlobalLayoutListener.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.CarouselViewRenderer+CarouselViewwOnGlobalLayoutListener, Microsoft.Maui.Controls.Compatibility", "", this, new java.lang.Object[] {  });
	}


	public void onGlobalLayout ()
	{
		n_onGlobalLayout ();
	}

	private native void n_onGlobalLayout ();

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
