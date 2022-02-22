package crc64338477404e88479c;


public class FormattedStringExtensions_FontSpan
	extends android.text.style.MetricAffectingSpan
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_updateDrawState:(Landroid/text/TextPaint;)V:GetUpdateDrawState_Landroid_text_TextPaint_Handler\n" +
			"n_updateMeasureState:(Landroid/text/TextPaint;)V:GetUpdateMeasureState_Landroid_text_TextPaint_Handler\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Controls.Platform.FormattedStringExtensions+FontSpan, Microsoft.Maui.Controls", FormattedStringExtensions_FontSpan.class, __md_methods);
	}


	public FormattedStringExtensions_FontSpan ()
	{
		super ();
		if (getClass () == FormattedStringExtensions_FontSpan.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Platform.FormattedStringExtensions+FontSpan, Microsoft.Maui.Controls", "", this, new java.lang.Object[] {  });
	}


	public void updateDrawState (android.text.TextPaint p0)
	{
		n_updateDrawState (p0);
	}

	private native void n_updateDrawState (android.text.TextPaint p0);


	public void updateMeasureState (android.text.TextPaint p0)
	{
		n_updateMeasureState (p0);
	}

	private native void n_updateMeasureState (android.text.TextPaint p0);

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
