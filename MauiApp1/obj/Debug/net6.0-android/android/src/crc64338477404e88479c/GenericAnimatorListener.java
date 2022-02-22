package crc64338477404e88479c;


public class GenericAnimatorListener
	extends android.animation.AnimatorListenerAdapter
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onAnimationCancel:(Landroid/animation/Animator;)V:GetOnAnimationCancel_Landroid_animation_Animator_Handler\n" +
			"n_onAnimationEnd:(Landroid/animation/Animator;)V:GetOnAnimationEnd_Landroid_animation_Animator_Handler\n" +
			"n_onAnimationRepeat:(Landroid/animation/Animator;)V:GetOnAnimationRepeat_Landroid_animation_Animator_Handler\n" +
			"n_finalize:()V:GetJavaFinalizeHandler\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Controls.Platform.GenericAnimatorListener, Microsoft.Maui.Controls", GenericAnimatorListener.class, __md_methods);
	}


	public GenericAnimatorListener ()
	{
		super ();
		if (getClass () == GenericAnimatorListener.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Platform.GenericAnimatorListener, Microsoft.Maui.Controls", "", this, new java.lang.Object[] {  });
	}


	public void onAnimationCancel (android.animation.Animator p0)
	{
		n_onAnimationCancel (p0);
	}

	private native void n_onAnimationCancel (android.animation.Animator p0);


	public void onAnimationEnd (android.animation.Animator p0)
	{
		n_onAnimationEnd (p0);
	}

	private native void n_onAnimationEnd (android.animation.Animator p0);


	public void onAnimationRepeat (android.animation.Animator p0)
	{
		n_onAnimationRepeat (p0);
	}

	private native void n_onAnimationRepeat (android.animation.Animator p0);


	public void finalize ()
	{
		n_finalize ();
	}

	private native void n_finalize ();

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
