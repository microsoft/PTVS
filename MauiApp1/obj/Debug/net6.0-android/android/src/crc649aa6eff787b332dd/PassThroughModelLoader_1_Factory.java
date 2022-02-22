package crc649aa6eff787b332dd;


public class PassThroughModelLoader_1_Factory
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		com.bumptech.glide.load.model.ModelLoaderFactory
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_build:(Lcom/bumptech/glide/load/model/MultiModelLoaderFactory;)Lcom/bumptech/glide/load/model/ModelLoader;:GetBuild_Lcom_bumptech_glide_load_model_MultiModelLoaderFactory_Handler:Bumptech.Glide.Load.Model.IModelLoaderFactoryInvoker, Xamarin.Android.Glide\n" +
			"n_teardown:()V:GetTeardownHandler:Bumptech.Glide.Load.Model.IModelLoaderFactoryInvoker, Xamarin.Android.Glide\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.BumptechGlide.PassThroughModelLoader`1+Factory, Microsoft.Maui", PassThroughModelLoader_1_Factory.class, __md_methods);
	}


	public PassThroughModelLoader_1_Factory ()
	{
		super ();
		if (getClass () == PassThroughModelLoader_1_Factory.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.BumptechGlide.PassThroughModelLoader`1+Factory, Microsoft.Maui", "", this, new java.lang.Object[] {  });
	}


	public com.bumptech.glide.load.model.ModelLoader build (com.bumptech.glide.load.model.MultiModelLoaderFactory p0)
	{
		return n_build (p0);
	}

	private native com.bumptech.glide.load.model.ModelLoader n_build (com.bumptech.glide.load.model.MultiModelLoaderFactory p0);


	public void teardown ()
	{
		n_teardown ();
	}

	private native void n_teardown ();

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
