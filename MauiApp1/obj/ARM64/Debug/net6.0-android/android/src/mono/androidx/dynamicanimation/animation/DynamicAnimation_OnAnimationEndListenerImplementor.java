package mono.androidx.dynamicanimation.animation;


public class DynamicAnimation_OnAnimationEndListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		androidx.dynamicanimation.animation.DynamicAnimation.OnAnimationEndListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onAnimationEnd:(Landroidx/dynamicanimation/animation/DynamicAnimation;ZFF)V:GetOnAnimationEnd_Landroidx_dynamicanimation_animation_DynamicAnimation_ZFFHandler:AndroidX.DynamicAnimation.DynamicAnimation/IOnAnimationEndListenerInvoker, Xamarin.AndroidX.DynamicAnimation\n" +
			"";
		mono.android.Runtime.register ("AndroidX.DynamicAnimation.DynamicAnimation+IOnAnimationEndListenerImplementor, Xamarin.AndroidX.DynamicAnimation", DynamicAnimation_OnAnimationEndListenerImplementor.class, __md_methods);
	}


	public DynamicAnimation_OnAnimationEndListenerImplementor ()
	{
		super ();
		if (getClass () == DynamicAnimation_OnAnimationEndListenerImplementor.class)
			mono.android.TypeManager.Activate ("AndroidX.DynamicAnimation.DynamicAnimation+IOnAnimationEndListenerImplementor, Xamarin.AndroidX.DynamicAnimation", "", this, new java.lang.Object[] {  });
	}


	public void onAnimationEnd (androidx.dynamicanimation.animation.DynamicAnimation p0, boolean p1, float p2, float p3)
	{
		n_onAnimationEnd (p0, p1, p2, p3);
	}

	private native void n_onAnimationEnd (androidx.dynamicanimation.animation.DynamicAnimation p0, boolean p1, float p2, float p3);

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
