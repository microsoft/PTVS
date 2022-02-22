package crc64192d9de59b079c6d;


public class ActivityLifecycleContextListener
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		android.app.Application.ActivityLifecycleCallbacks
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onActivityCreated:(Landroid/app/Activity;Landroid/os/Bundle;)V:GetOnActivityCreated_Landroid_app_Activity_Landroid_os_Bundle_Handler:Android.App.Application/IActivityLifecycleCallbacksInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"n_onActivityDestroyed:(Landroid/app/Activity;)V:GetOnActivityDestroyed_Landroid_app_Activity_Handler:Android.App.Application/IActivityLifecycleCallbacksInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"n_onActivityPaused:(Landroid/app/Activity;)V:GetOnActivityPaused_Landroid_app_Activity_Handler:Android.App.Application/IActivityLifecycleCallbacksInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"n_onActivityResumed:(Landroid/app/Activity;)V:GetOnActivityResumed_Landroid_app_Activity_Handler:Android.App.Application/IActivityLifecycleCallbacksInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"n_onActivitySaveInstanceState:(Landroid/app/Activity;Landroid/os/Bundle;)V:GetOnActivitySaveInstanceState_Landroid_app_Activity_Landroid_os_Bundle_Handler:Android.App.Application/IActivityLifecycleCallbacksInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"n_onActivityStarted:(Landroid/app/Activity;)V:GetOnActivityStarted_Landroid_app_Activity_Handler:Android.App.Application/IActivityLifecycleCallbacksInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"n_onActivityStopped:(Landroid/app/Activity;)V:GetOnActivityStopped_Landroid_app_Activity_Handler:Android.App.Application/IActivityLifecycleCallbacksInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"n_onActivityPostCreated:(Landroid/app/Activity;Landroid/os/Bundle;)V:GetOnActivityPostCreated_Landroid_app_Activity_Landroid_os_Bundle_Handler:Android.App.Application/IActivityLifecycleCallbacks, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"n_onActivityPostDestroyed:(Landroid/app/Activity;)V:GetOnActivityPostDestroyed_Landroid_app_Activity_Handler:Android.App.Application/IActivityLifecycleCallbacks, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"n_onActivityPostPaused:(Landroid/app/Activity;)V:GetOnActivityPostPaused_Landroid_app_Activity_Handler:Android.App.Application/IActivityLifecycleCallbacks, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"n_onActivityPostResumed:(Landroid/app/Activity;)V:GetOnActivityPostResumed_Landroid_app_Activity_Handler:Android.App.Application/IActivityLifecycleCallbacks, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"n_onActivityPostSaveInstanceState:(Landroid/app/Activity;Landroid/os/Bundle;)V:GetOnActivityPostSaveInstanceState_Landroid_app_Activity_Landroid_os_Bundle_Handler:Android.App.Application/IActivityLifecycleCallbacks, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"n_onActivityPostStarted:(Landroid/app/Activity;)V:GetOnActivityPostStarted_Landroid_app_Activity_Handler:Android.App.Application/IActivityLifecycleCallbacks, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"n_onActivityPostStopped:(Landroid/app/Activity;)V:GetOnActivityPostStopped_Landroid_app_Activity_Handler:Android.App.Application/IActivityLifecycleCallbacks, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"n_onActivityPreCreated:(Landroid/app/Activity;Landroid/os/Bundle;)V:GetOnActivityPreCreated_Landroid_app_Activity_Landroid_os_Bundle_Handler:Android.App.Application/IActivityLifecycleCallbacks, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"n_onActivityPreDestroyed:(Landroid/app/Activity;)V:GetOnActivityPreDestroyed_Landroid_app_Activity_Handler:Android.App.Application/IActivityLifecycleCallbacks, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"n_onActivityPrePaused:(Landroid/app/Activity;)V:GetOnActivityPrePaused_Landroid_app_Activity_Handler:Android.App.Application/IActivityLifecycleCallbacks, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"n_onActivityPreResumed:(Landroid/app/Activity;)V:GetOnActivityPreResumed_Landroid_app_Activity_Handler:Android.App.Application/IActivityLifecycleCallbacks, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"n_onActivityPreSaveInstanceState:(Landroid/app/Activity;Landroid/os/Bundle;)V:GetOnActivityPreSaveInstanceState_Landroid_app_Activity_Landroid_os_Bundle_Handler:Android.App.Application/IActivityLifecycleCallbacks, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"n_onActivityPreStarted:(Landroid/app/Activity;)V:GetOnActivityPreStarted_Landroid_app_Activity_Handler:Android.App.Application/IActivityLifecycleCallbacks, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"n_onActivityPreStopped:(Landroid/app/Activity;)V:GetOnActivityPreStopped_Landroid_app_Activity_Handler:Android.App.Application/IActivityLifecycleCallbacks, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Essentials.ActivityLifecycleContextListener, Microsoft.Maui.Essentials", ActivityLifecycleContextListener.class, __md_methods);
	}


	public ActivityLifecycleContextListener ()
	{
		super ();
		if (getClass () == ActivityLifecycleContextListener.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Essentials.ActivityLifecycleContextListener, Microsoft.Maui.Essentials", "", this, new java.lang.Object[] {  });
	}


	public void onActivityCreated (android.app.Activity p0, android.os.Bundle p1)
	{
		n_onActivityCreated (p0, p1);
	}

	private native void n_onActivityCreated (android.app.Activity p0, android.os.Bundle p1);


	public void onActivityDestroyed (android.app.Activity p0)
	{
		n_onActivityDestroyed (p0);
	}

	private native void n_onActivityDestroyed (android.app.Activity p0);


	public void onActivityPaused (android.app.Activity p0)
	{
		n_onActivityPaused (p0);
	}

	private native void n_onActivityPaused (android.app.Activity p0);


	public void onActivityResumed (android.app.Activity p0)
	{
		n_onActivityResumed (p0);
	}

	private native void n_onActivityResumed (android.app.Activity p0);


	public void onActivitySaveInstanceState (android.app.Activity p0, android.os.Bundle p1)
	{
		n_onActivitySaveInstanceState (p0, p1);
	}

	private native void n_onActivitySaveInstanceState (android.app.Activity p0, android.os.Bundle p1);


	public void onActivityStarted (android.app.Activity p0)
	{
		n_onActivityStarted (p0);
	}

	private native void n_onActivityStarted (android.app.Activity p0);


	public void onActivityStopped (android.app.Activity p0)
	{
		n_onActivityStopped (p0);
	}

	private native void n_onActivityStopped (android.app.Activity p0);


	public void onActivityPostCreated (android.app.Activity p0, android.os.Bundle p1)
	{
		n_onActivityPostCreated (p0, p1);
	}

	private native void n_onActivityPostCreated (android.app.Activity p0, android.os.Bundle p1);


	public void onActivityPostDestroyed (android.app.Activity p0)
	{
		n_onActivityPostDestroyed (p0);
	}

	private native void n_onActivityPostDestroyed (android.app.Activity p0);


	public void onActivityPostPaused (android.app.Activity p0)
	{
		n_onActivityPostPaused (p0);
	}

	private native void n_onActivityPostPaused (android.app.Activity p0);


	public void onActivityPostResumed (android.app.Activity p0)
	{
		n_onActivityPostResumed (p0);
	}

	private native void n_onActivityPostResumed (android.app.Activity p0);


	public void onActivityPostSaveInstanceState (android.app.Activity p0, android.os.Bundle p1)
	{
		n_onActivityPostSaveInstanceState (p0, p1);
	}

	private native void n_onActivityPostSaveInstanceState (android.app.Activity p0, android.os.Bundle p1);


	public void onActivityPostStarted (android.app.Activity p0)
	{
		n_onActivityPostStarted (p0);
	}

	private native void n_onActivityPostStarted (android.app.Activity p0);


	public void onActivityPostStopped (android.app.Activity p0)
	{
		n_onActivityPostStopped (p0);
	}

	private native void n_onActivityPostStopped (android.app.Activity p0);


	public void onActivityPreCreated (android.app.Activity p0, android.os.Bundle p1)
	{
		n_onActivityPreCreated (p0, p1);
	}

	private native void n_onActivityPreCreated (android.app.Activity p0, android.os.Bundle p1);


	public void onActivityPreDestroyed (android.app.Activity p0)
	{
		n_onActivityPreDestroyed (p0);
	}

	private native void n_onActivityPreDestroyed (android.app.Activity p0);


	public void onActivityPrePaused (android.app.Activity p0)
	{
		n_onActivityPrePaused (p0);
	}

	private native void n_onActivityPrePaused (android.app.Activity p0);


	public void onActivityPreResumed (android.app.Activity p0)
	{
		n_onActivityPreResumed (p0);
	}

	private native void n_onActivityPreResumed (android.app.Activity p0);


	public void onActivityPreSaveInstanceState (android.app.Activity p0, android.os.Bundle p1)
	{
		n_onActivityPreSaveInstanceState (p0, p1);
	}

	private native void n_onActivityPreSaveInstanceState (android.app.Activity p0, android.os.Bundle p1);


	public void onActivityPreStarted (android.app.Activity p0)
	{
		n_onActivityPreStarted (p0);
	}

	private native void n_onActivityPreStarted (android.app.Activity p0);


	public void onActivityPreStopped (android.app.Activity p0)
	{
		n_onActivityPreStopped (p0);
	}

	private native void n_onActivityPreStopped (android.app.Activity p0);

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
