package crc649aa6eff787b332dd;


public class FontImageSourceDecoder
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		com.bumptech.glide.load.ResourceDecoder
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_decode:(Ljava/lang/Object;IILcom/bumptech/glide/load/Options;)Lcom/bumptech/glide/load/engine/Resource;:GetDecode_Ljava_lang_Object_IILcom_bumptech_glide_load_Options_Handler:Bumptech.Glide.Load.IResourceDecoderInvoker, Xamarin.Android.Glide\n" +
			"n_handles:(Ljava/lang/Object;Lcom/bumptech/glide/load/Options;)Z:GetHandles_Ljava_lang_Object_Lcom_bumptech_glide_load_Options_Handler:Bumptech.Glide.Load.IResourceDecoderInvoker, Xamarin.Android.Glide\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.BumptechGlide.FontImageSourceDecoder, Microsoft.Maui", FontImageSourceDecoder.class, __md_methods);
	}


	public FontImageSourceDecoder ()
	{
		super ();
		if (getClass () == FontImageSourceDecoder.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.BumptechGlide.FontImageSourceDecoder, Microsoft.Maui", "", this, new java.lang.Object[] {  });
	}


	public com.bumptech.glide.load.engine.Resource decode (java.lang.Object p0, int p1, int p2, com.bumptech.glide.load.Options p3)
	{
		return n_decode (p0, p1, p2, p3);
	}

	private native com.bumptech.glide.load.engine.Resource n_decode (java.lang.Object p0, int p1, int p2, com.bumptech.glide.load.Options p3);


	public boolean handles (java.lang.Object p0, com.bumptech.glide.load.Options p1)
	{
		return n_handles (p0, p1);
	}

	private native boolean n_handles (java.lang.Object p0, com.bumptech.glide.load.Options p1);

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
