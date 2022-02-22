package mono.androidx.constraintlayout.motion.widget;


public class MotionLayout_TransitionListenerImplementor
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		androidx.constraintlayout.motion.widget.MotionLayout.TransitionListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onTransitionChange:(Landroidx/constraintlayout/motion/widget/MotionLayout;IIF)V:GetOnTransitionChange_Landroidx_constraintlayout_motion_widget_MotionLayout_IIFHandler:AndroidX.ConstraintLayout.Motion.Widget.MotionLayout/ITransitionListenerInvoker, Xamarin.AndroidX.ConstraintLayout\n" +
			"n_onTransitionCompleted:(Landroidx/constraintlayout/motion/widget/MotionLayout;I)V:GetOnTransitionCompleted_Landroidx_constraintlayout_motion_widget_MotionLayout_IHandler:AndroidX.ConstraintLayout.Motion.Widget.MotionLayout/ITransitionListenerInvoker, Xamarin.AndroidX.ConstraintLayout\n" +
			"n_onTransitionStarted:(Landroidx/constraintlayout/motion/widget/MotionLayout;II)V:GetOnTransitionStarted_Landroidx_constraintlayout_motion_widget_MotionLayout_IIHandler:AndroidX.ConstraintLayout.Motion.Widget.MotionLayout/ITransitionListenerInvoker, Xamarin.AndroidX.ConstraintLayout\n" +
			"n_onTransitionTrigger:(Landroidx/constraintlayout/motion/widget/MotionLayout;IZF)V:GetOnTransitionTrigger_Landroidx_constraintlayout_motion_widget_MotionLayout_IZFHandler:AndroidX.ConstraintLayout.Motion.Widget.MotionLayout/ITransitionListenerInvoker, Xamarin.AndroidX.ConstraintLayout\n" +
			"";
		mono.android.Runtime.register ("AndroidX.ConstraintLayout.Motion.Widget.MotionLayout+ITransitionListenerImplementor, Xamarin.AndroidX.ConstraintLayout", MotionLayout_TransitionListenerImplementor.class, __md_methods);
	}


	public MotionLayout_TransitionListenerImplementor ()
	{
		super ();
		if (getClass () == MotionLayout_TransitionListenerImplementor.class)
			mono.android.TypeManager.Activate ("AndroidX.ConstraintLayout.Motion.Widget.MotionLayout+ITransitionListenerImplementor, Xamarin.AndroidX.ConstraintLayout", "", this, new java.lang.Object[] {  });
	}


	public void onTransitionChange (androidx.constraintlayout.motion.widget.MotionLayout p0, int p1, int p2, float p3)
	{
		n_onTransitionChange (p0, p1, p2, p3);
	}

	private native void n_onTransitionChange (androidx.constraintlayout.motion.widget.MotionLayout p0, int p1, int p2, float p3);


	public void onTransitionCompleted (androidx.constraintlayout.motion.widget.MotionLayout p0, int p1)
	{
		n_onTransitionCompleted (p0, p1);
	}

	private native void n_onTransitionCompleted (androidx.constraintlayout.motion.widget.MotionLayout p0, int p1);


	public void onTransitionStarted (androidx.constraintlayout.motion.widget.MotionLayout p0, int p1, int p2)
	{
		n_onTransitionStarted (p0, p1, p2);
	}

	private native void n_onTransitionStarted (androidx.constraintlayout.motion.widget.MotionLayout p0, int p1, int p2);


	public void onTransitionTrigger (androidx.constraintlayout.motion.widget.MotionLayout p0, int p1, boolean p2, float p3)
	{
		n_onTransitionTrigger (p0, p1, p2, p3);
	}

	private native void n_onTransitionTrigger (androidx.constraintlayout.motion.widget.MotionLayout p0, int p1, boolean p2, float p3);

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
