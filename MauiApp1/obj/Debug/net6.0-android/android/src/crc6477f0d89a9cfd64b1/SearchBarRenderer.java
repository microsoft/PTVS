package crc6477f0d89a9cfd64b1;


public class SearchBarRenderer
	extends crc6477f0d89a9cfd64b1.ViewRenderer_2
	implements
		mono.android.IGCUserPeer,
		android.widget.SearchView.OnQueryTextListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onQueryTextChange:(Ljava/lang/String;)Z:GetOnQueryTextChange_Ljava_lang_String_Handler:Android.Widget.SearchView/IOnQueryTextListenerInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"n_onQueryTextSubmit:(Ljava/lang/String;)Z:GetOnQueryTextSubmit_Ljava_lang_String_Handler:Android.Widget.SearchView/IOnQueryTextListenerInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Controls.Compatibility.Platform.Android.SearchBarRenderer, Microsoft.Maui.Controls.Compatibility", SearchBarRenderer.class, __md_methods);
	}


	public SearchBarRenderer (android.content.Context p0)
	{
		super (p0);
		if (getClass () == SearchBarRenderer.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.SearchBarRenderer, Microsoft.Maui.Controls.Compatibility", "Android.Content.Context, Mono.Android", this, new java.lang.Object[] { p0 });
	}


	public SearchBarRenderer (android.content.Context p0, android.util.AttributeSet p1)
	{
		super (p0, p1);
		if (getClass () == SearchBarRenderer.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.SearchBarRenderer, Microsoft.Maui.Controls.Compatibility", "Android.Content.Context, Mono.Android:Android.Util.IAttributeSet, Mono.Android", this, new java.lang.Object[] { p0, p1 });
	}


	public SearchBarRenderer (android.content.Context p0, android.util.AttributeSet p1, int p2)
	{
		super (p0, p1, p2);
		if (getClass () == SearchBarRenderer.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.SearchBarRenderer, Microsoft.Maui.Controls.Compatibility", "Android.Content.Context, Mono.Android:Android.Util.IAttributeSet, Mono.Android:System.Int32, System.Private.CoreLib", this, new java.lang.Object[] { p0, p1, p2 });
	}


	public boolean onQueryTextChange (java.lang.String p0)
	{
		return n_onQueryTextChange (p0);
	}

	private native boolean n_onQueryTextChange (java.lang.String p0);


	public boolean onQueryTextSubmit (java.lang.String p0)
	{
		return n_onQueryTextSubmit (p0);
	}

	private native boolean n_onQueryTextSubmit (java.lang.String p0);

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
