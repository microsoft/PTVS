package mono.androidx.transition;


public class Transition_TransitionListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		androidx.transition.Transition.TransitionListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onTransitionCancel:(Landroidx/transition/Transition;)V:GetOnTransitionCancel_Landroidx_transition_Transition_Handler:AndroidX.Transitions.Transition/ITransitionListenerInvoker, Xamarin.AndroidX.Transition\n" +
			"n_onTransitionEnd:(Landroidx/transition/Transition;)V:GetOnTransitionEnd_Landroidx_transition_Transition_Handler:AndroidX.Transitions.Transition/ITransitionListenerInvoker, Xamarin.AndroidX.Transition\n" +
			"n_onTransitionPause:(Landroidx/transition/Transition;)V:GetOnTransitionPause_Landroidx_transition_Transition_Handler:AndroidX.Transitions.Transition/ITransitionListenerInvoker, Xamarin.AndroidX.Transition\n" +
			"n_onTransitionResume:(Landroidx/transition/Transition;)V:GetOnTransitionResume_Landroidx_transition_Transition_Handler:AndroidX.Transitions.Transition/ITransitionListenerInvoker, Xamarin.AndroidX.Transition\n" +
			"n_onTransitionStart:(Landroidx/transition/Transition;)V:GetOnTransitionStart_Landroidx_transition_Transition_Handler:AndroidX.Transitions.Transition/ITransitionListenerInvoker, Xamarin.AndroidX.Transition\n" +
			"";
		mono.android.Runtime.register ("AndroidX.Transitions.Transition+ITransitionListenerImplementor, Xamarin.AndroidX.Transition", Transition_TransitionListenerImplementor.class, __md_methods);
	}


	public Transition_TransitionListenerImplementor ()
	{
		super ();
		if (getClass () == Transition_TransitionListenerImplementor.class)
			mono.android.TypeManager.Activate ("AndroidX.Transitions.Transition+ITransitionListenerImplementor, Xamarin.AndroidX.Transition", "", this, new java.lang.Object[] {  });
	}


	public void onTransitionCancel (androidx.transition.Transition p0)
	{
		n_onTransitionCancel (p0);
	}

	private native void n_onTransitionCancel (androidx.transition.Transition p0);


	public void onTransitionEnd (androidx.transition.Transition p0)
	{
		n_onTransitionEnd (p0);
	}

	private native void n_onTransitionEnd (androidx.transition.Transition p0);


	public void onTransitionPause (androidx.transition.Transition p0)
	{
		n_onTransitionPause (p0);
	}

	private native void n_onTransitionPause (androidx.transition.Transition p0);


	public void onTransitionResume (androidx.transition.Transition p0)
	{
		n_onTransitionResume (p0);
	}

	private native void n_onTransitionResume (androidx.transition.Transition p0);


	public void onTransitionStart (androidx.transition.Transition p0)
	{
		n_onTransitionStart (p0);
	}

	private native void n_onTransitionStart (androidx.transition.Transition p0);

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
