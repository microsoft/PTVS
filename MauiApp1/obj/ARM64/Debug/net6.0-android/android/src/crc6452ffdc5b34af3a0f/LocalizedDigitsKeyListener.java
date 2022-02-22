package crc6452ffdc5b34af3a0f;


public class LocalizedDigitsKeyListener
	extends android.text.method.NumberKeyListener
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_getInputType:()I:GetGetInputTypeHandler\n" +
			"n_getAcceptedChars:()[C:GetGetAcceptedCharsHandler\n" +
			"n_filter:(Ljava/lang/CharSequence;IILandroid/text/Spanned;II)Ljava/lang/CharSequence;:GetFilter_Ljava_lang_CharSequence_IILandroid_text_Spanned_IIHandler\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Platform.LocalizedDigitsKeyListener, Microsoft.Maui", LocalizedDigitsKeyListener.class, __md_methods);
	}


	public LocalizedDigitsKeyListener ()
	{
		super ();
		if (getClass () == LocalizedDigitsKeyListener.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Platform.LocalizedDigitsKeyListener, Microsoft.Maui", "", this, new java.lang.Object[] {  });
	}

	public LocalizedDigitsKeyListener (int p0, char p1)
	{
		super ();
		if (getClass () == LocalizedDigitsKeyListener.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Platform.LocalizedDigitsKeyListener, Microsoft.Maui", "Android.Text.InputTypes, Mono.Android:System.Char, System.Private.CoreLib", this, new java.lang.Object[] { p0, p1 });
	}


	public int getInputType ()
	{
		return n_getInputType ();
	}

	private native int n_getInputType ();


	public char[] getAcceptedChars ()
	{
		return n_getAcceptedChars ();
	}

	private native char[] n_getAcceptedChars ();


	public java.lang.CharSequence filter (java.lang.CharSequence p0, int p1, int p2, android.text.Spanned p3, int p4, int p5)
	{
		return n_filter (p0, p1, p2, p3, p4, p5);
	}

	private native java.lang.CharSequence n_filter (java.lang.CharSequence p0, int p1, int p2, android.text.Spanned p3, int p4, int p5);

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
