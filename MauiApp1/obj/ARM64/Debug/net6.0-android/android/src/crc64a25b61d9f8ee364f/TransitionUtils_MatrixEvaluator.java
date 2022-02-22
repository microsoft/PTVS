package crc64a25b61d9f8ee364f;


public class TransitionUtils_MatrixEvaluator
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		android.animation.TypeEvaluator
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_evaluate:(FLjava/lang/Object;Ljava/lang/Object;)Ljava/lang/Object;:GetEvaluate_FLjava_lang_Object_Ljava_lang_Object_Handler:Android.Animation.ITypeEvaluatorInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"";
		mono.android.Runtime.register ("AndroidX.Transitions.TransitionUtils+MatrixEvaluator, Xamarin.AndroidX.Transition", TransitionUtils_MatrixEvaluator.class, __md_methods);
	}


	public TransitionUtils_MatrixEvaluator ()
	{
		super ();
		if (getClass () == TransitionUtils_MatrixEvaluator.class)
			mono.android.TypeManager.Activate ("AndroidX.Transitions.TransitionUtils+MatrixEvaluator, Xamarin.AndroidX.Transition", "", this, new java.lang.Object[] {  });
	}


	public java.lang.Object evaluate (float p0, java.lang.Object p1, java.lang.Object p2)
	{
		return n_evaluate (p0, p1, p2);
	}

	private native java.lang.Object n_evaluate (float p0, java.lang.Object p1, java.lang.Object p2);

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
