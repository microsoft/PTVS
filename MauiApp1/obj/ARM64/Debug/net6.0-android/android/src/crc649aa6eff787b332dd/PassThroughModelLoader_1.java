package crc649aa6eff787b332dd;


public class PassThroughModelLoader_1
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		com.bumptech.glide.load.model.ModelLoader
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_buildLoadData:(Ljava/lang/Object;IILcom/bumptech/glide/load/Options;)Lcom/bumptech/glide/load/model/ModelLoader$LoadData;:GetBuildLoadData_Ljava_lang_Object_IILcom_bumptech_glide_load_Options_Handler:Bumptech.Glide.Load.Model.IModelLoaderInvoker, Xamarin.Android.Glide\n" +
			"n_handles:(Ljava/lang/Object;)Z:GetHandles_Ljava_lang_Object_Handler:Bumptech.Glide.Load.Model.IModelLoaderInvoker, Xamarin.Android.Glide\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.BumptechGlide.PassThroughModelLoader`1, Microsoft.Maui", PassThroughModelLoader_1.class, __md_methods);
	}


	public PassThroughModelLoader_1 ()
	{
		super ();
		if (getClass () == PassThroughModelLoader_1.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.BumptechGlide.PassThroughModelLoader`1, Microsoft.Maui", "", this, new java.lang.Object[] {  });
	}


	public com.bumptech.glide.load.model.ModelLoader.LoadData buildLoadData (java.lang.Object p0, int p1, int p2, com.bumptech.glide.load.Options p3)
	{
		return n_buildLoadData (p0, p1, p2, p3);
	}

	private native com.bumptech.glide.load.model.ModelLoader.LoadData n_buildLoadData (java.lang.Object p0, int p1, int p2, com.bumptech.glide.load.Options p3);


	public boolean handles (java.lang.Object p0)
	{
		return n_handles (p0);
	}

	private native boolean n_handles (java.lang.Object p0);

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
