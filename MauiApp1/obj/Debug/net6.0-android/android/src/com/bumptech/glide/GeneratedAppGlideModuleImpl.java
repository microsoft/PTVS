package com.bumptech.glide;


public class GeneratedAppGlideModuleImpl
	extends com.bumptech.glide.GeneratedAppGlideModule
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_registerComponents:(Landroid/content/Context;Lcom/bumptech/glide/Glide;Lcom/bumptech/glide/Registry;)V:GetRegisterComponents_Landroid_content_Context_Lcom_bumptech_glide_Glide_Lcom_bumptech_glide_Registry_Handler\n" +
			"n_getExcludedModuleClasses:()Ljava/util/Set;:GetGetExcludedModuleClassesHandler\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.BumptechGlide.MauiAppGlideModule, Microsoft.Maui", GeneratedAppGlideModuleImpl.class, __md_methods);
	}


	public GeneratedAppGlideModuleImpl ()
	{
		super ();
		if (getClass () == GeneratedAppGlideModuleImpl.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.BumptechGlide.MauiAppGlideModule, Microsoft.Maui", "", this, new java.lang.Object[] {  });
	}

	public GeneratedAppGlideModuleImpl (android.content.Context p0)
	{
		super ();
		if (getClass () == GeneratedAppGlideModuleImpl.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.BumptechGlide.MauiAppGlideModule, Microsoft.Maui", "Android.Content.Context, Mono.Android", this, new java.lang.Object[] { p0 });
	}


	public void registerComponents (android.content.Context p0, com.bumptech.glide.Glide p1, com.bumptech.glide.Registry p2)
	{
		n_registerComponents (p0, p1, p2);
	}

	private native void n_registerComponents (android.content.Context p0, com.bumptech.glide.Glide p1, com.bumptech.glide.Registry p2);


	public java.util.Set getExcludedModuleClasses ()
	{
		return n_getExcludedModuleClasses ();
	}

	private native java.util.Set n_getExcludedModuleClasses ();

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
