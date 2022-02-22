package mono.com.bumptech.glide.request;


public class RequestListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		com.bumptech.glide.request.RequestListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onLoadFailed:(Lcom/bumptech/glide/load/engine/GlideException;Ljava/lang/Object;Lcom/bumptech/glide/request/target/Target;Z)Z:GetOnLoadFailed_Lcom_bumptech_glide_load_engine_GlideException_Ljava_lang_Object_Lcom_bumptech_glide_request_target_Target_ZHandler:Bumptech.Glide.Request.IRequestListenerInvoker, Xamarin.Android.Glide\n" +
			"n_onResourceReady:(Ljava/lang/Object;Ljava/lang/Object;Lcom/bumptech/glide/request/target/Target;Lcom/bumptech/glide/load/DataSource;Z)Z:GetOnResourceReady_Ljava_lang_Object_Ljava_lang_Object_Lcom_bumptech_glide_request_target_Target_Lcom_bumptech_glide_load_DataSource_ZHandler:Bumptech.Glide.Request.IRequestListenerInvoker, Xamarin.Android.Glide\n" +
			"";
		mono.android.Runtime.register ("Bumptech.Glide.Request.IRequestListenerImplementor, Xamarin.Android.Glide", RequestListenerImplementor.class, __md_methods);
	}


	public RequestListenerImplementor ()
	{
		super ();
		if (getClass () == RequestListenerImplementor.class)
			mono.android.TypeManager.Activate ("Bumptech.Glide.Request.IRequestListenerImplementor, Xamarin.Android.Glide", "", this, new java.lang.Object[] {  });
	}


	public boolean onLoadFailed (com.bumptech.glide.load.engine.GlideException p0, java.lang.Object p1, com.bumptech.glide.request.target.Target p2, boolean p3)
	{
		return n_onLoadFailed (p0, p1, p2, p3);
	}

	private native boolean n_onLoadFailed (com.bumptech.glide.load.engine.GlideException p0, java.lang.Object p1, com.bumptech.glide.request.target.Target p2, boolean p3);


	public boolean onResourceReady (java.lang.Object p0, java.lang.Object p1, com.bumptech.glide.request.target.Target p2, com.bumptech.glide.load.DataSource p3, boolean p4)
	{
		return n_onResourceReady (p0, p1, p2, p3, p4);
	}

	private native boolean n_onResourceReady (java.lang.Object p0, java.lang.Object p1, com.bumptech.glide.request.target.Target p2, com.bumptech.glide.load.DataSource p3, boolean p4);

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
