package crc6488302ad6e9e4df1a;


public class MauiAppCompatActivity
	extends androidx.appcompat.app.AppCompatActivity
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onCreate:(Landroid/os/Bundle;)V:GetOnCreate_Landroid_os_Bundle_Handler\n" +
			"n_onActivityResult:(IILandroid/content/Intent;)V:GetOnActivityResult_IILandroid_content_Intent_Handler\n" +
			"n_onBackPressed:()V:GetOnBackPressedHandler\n" +
			"n_onConfigurationChanged:(Landroid/content/res/Configuration;)V:GetOnConfigurationChanged_Landroid_content_res_Configuration_Handler\n" +
			"n_onNewIntent:(Landroid/content/Intent;)V:GetOnNewIntent_Landroid_content_Intent_Handler\n" +
			"n_onPostCreate:(Landroid/os/Bundle;)V:GetOnPostCreate_Landroid_os_Bundle_Handler\n" +
			"n_onPostResume:()V:GetOnPostResumeHandler\n" +
			"n_onRestart:()V:GetOnRestartHandler\n" +
			"n_onRequestPermissionsResult:(I[Ljava/lang/String;[I)V:GetOnRequestPermissionsResult_IarrayLjava_lang_String_arrayIHandler\n" +
			"n_onRestoreInstanceState:(Landroid/os/Bundle;)V:GetOnRestoreInstanceState_Landroid_os_Bundle_Handler\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.MauiAppCompatActivity, Microsoft.Maui", MauiAppCompatActivity.class, __md_methods);
	}


	public MauiAppCompatActivity ()
	{
		super ();
		if (getClass () == MauiAppCompatActivity.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.MauiAppCompatActivity, Microsoft.Maui", "", this, new java.lang.Object[] {  });
	}


	public MauiAppCompatActivity (int p0)
	{
		super (p0);
		if (getClass () == MauiAppCompatActivity.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.MauiAppCompatActivity, Microsoft.Maui", "System.Int32, System.Private.CoreLib", this, new java.lang.Object[] { p0 });
	}


	public void onCreate (android.os.Bundle p0)
	{
		n_onCreate (p0);
	}

	private native void n_onCreate (android.os.Bundle p0);


	public void onActivityResult (int p0, int p1, android.content.Intent p2)
	{
		n_onActivityResult (p0, p1, p2);
	}

	private native void n_onActivityResult (int p0, int p1, android.content.Intent p2);


	public void onBackPressed ()
	{
		n_onBackPressed ();
	}

	private native void n_onBackPressed ();


	public void onConfigurationChanged (android.content.res.Configuration p0)
	{
		n_onConfigurationChanged (p0);
	}

	private native void n_onConfigurationChanged (android.content.res.Configuration p0);


	public void onNewIntent (android.content.Intent p0)
	{
		n_onNewIntent (p0);
	}

	private native void n_onNewIntent (android.content.Intent p0);


	public void onPostCreate (android.os.Bundle p0)
	{
		n_onPostCreate (p0);
	}

	private native void n_onPostCreate (android.os.Bundle p0);


	public void onPostResume ()
	{
		n_onPostResume ();
	}

	private native void n_onPostResume ();


	public void onRestart ()
	{
		n_onRestart ();
	}

	private native void n_onRestart ();


	public void onRequestPermissionsResult (int p0, java.lang.String[] p1, int[] p2)
	{
		n_onRequestPermissionsResult (p0, p1, p2);
	}

	private native void n_onRequestPermissionsResult (int p0, java.lang.String[] p1, int[] p2);


	public void onRestoreInstanceState (android.os.Bundle p0)
	{
		n_onRestoreInstanceState (p0);
	}

	private native void n_onRestoreInstanceState (android.os.Bundle p0);

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
