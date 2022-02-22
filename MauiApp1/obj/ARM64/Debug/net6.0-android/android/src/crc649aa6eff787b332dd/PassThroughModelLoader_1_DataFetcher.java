package crc649aa6eff787b332dd;


public class PassThroughModelLoader_1_DataFetcher
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		com.bumptech.glide.load.data.DataFetcher
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_getDataClass:()Ljava/lang/Class;:GetGetDataClassHandler:Bumptech.Glide.Load.Data.IDataFetcherInvoker, Xamarin.Android.Glide\n" +
			"n_getDataSource:()Lcom/bumptech/glide/load/DataSource;:GetGetDataSourceHandler:Bumptech.Glide.Load.Data.IDataFetcherInvoker, Xamarin.Android.Glide\n" +
			"n_cancel:()V:GetCancelHandler:Bumptech.Glide.Load.Data.IDataFetcherInvoker, Xamarin.Android.Glide\n" +
			"n_cleanup:()V:GetCleanupHandler:Bumptech.Glide.Load.Data.IDataFetcherInvoker, Xamarin.Android.Glide\n" +
			"n_loadData:(Lcom/bumptech/glide/Priority;Lcom/bumptech/glide/load/data/DataFetcher$DataCallback;)V:GetLoadData_Lcom_bumptech_glide_Priority_Lcom_bumptech_glide_load_data_DataFetcher_DataCallback_Handler:Bumptech.Glide.Load.Data.IDataFetcherInvoker, Xamarin.Android.Glide\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.BumptechGlide.PassThroughModelLoader`1+DataFetcher, Microsoft.Maui", PassThroughModelLoader_1_DataFetcher.class, __md_methods);
	}


	public PassThroughModelLoader_1_DataFetcher ()
	{
		super ();
		if (getClass () == PassThroughModelLoader_1_DataFetcher.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.BumptechGlide.PassThroughModelLoader`1+DataFetcher, Microsoft.Maui", "", this, new java.lang.Object[] {  });
	}

	public PassThroughModelLoader_1_DataFetcher (java.lang.Object p0)
	{
		super ();
		if (getClass () == PassThroughModelLoader_1_DataFetcher.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.BumptechGlide.PassThroughModelLoader`1+DataFetcher, Microsoft.Maui", "Java.Lang.Object, Mono.Android", this, new java.lang.Object[] { p0 });
	}


	public java.lang.Class getDataClass ()
	{
		return n_getDataClass ();
	}

	private native java.lang.Class n_getDataClass ();


	public com.bumptech.glide.load.DataSource getDataSource ()
	{
		return n_getDataSource ();
	}

	private native com.bumptech.glide.load.DataSource n_getDataSource ();


	public void cancel ()
	{
		n_cancel ();
	}

	private native void n_cancel ();


	public void cleanup ()
	{
		n_cleanup ();
	}

	private native void n_cleanup ();


	public void loadData (com.bumptech.glide.Priority p0, com.bumptech.glide.load.data.DataFetcher.DataCallback p1)
	{
		n_loadData (p0, p1);
	}

	private native void n_loadData (com.bumptech.glide.Priority p0, com.bumptech.glide.load.data.DataFetcher.DataCallback p1);

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
