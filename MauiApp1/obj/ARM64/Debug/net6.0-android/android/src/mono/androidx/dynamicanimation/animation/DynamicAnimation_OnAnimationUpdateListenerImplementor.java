package mono.androidx.dynamicanimation.animation;


public class DynamicAnimation_OnAnimationUpdateListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		androidx.dynamicanimation.animation.DynamicAnimation.OnAnimationUpdateListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onAnimationUpdate:(Landroidx/dynamicanimation/animation/DynamicAnimation;FF)V:GetOnAnimationUpdate_Landroidx_dynamicanimation_animation_DynamicAnimation_FFHandler:AndroidX.DynamicAnimation.DynamicAnimation/IOnAnimationUpdateListenerInvoker, Xamarin.AndroidX.DynamicAnimation\n" +
			"";
		mono.android.Runtime.register ("AndroidX.DynamicAnimation.DynamicAnimation+IOnAnimationUpdateListenerImplementor, Xamarin.AndroidX.DynamicAnimation", DynamicAnimation_OnAnimationUpdateListenerImplementor.class, __md_methods);
	}


	public DynamicAnimation_OnAnimationUpdateListenerImplementor ()
	{
		super ();
		if (getClass () == DynamicAnimation_OnAnimationUpdateListenerImplementor.class)
			mono.android.TypeManager.Activate ("AndroidX.DynamicAnimation.DynamicAnimation+IOnAnimationUpdateListenerImplementor, Xamarin.AndroidX.DynamicAnimation", "", this, new java.lang.Object[] {  });
	}


	public void onAnimationUpdate (androidx.dynamicanimation.animation.DynamicAnimation p0, float p1, float p2)
	{
		n_onAnimationUpdate (p0, p1, p2);
	}

	private native void n_onAnimationUpdate (androidx.dynamicanimation.animation.DynamicAnimation p0, float p1, float p2);

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
