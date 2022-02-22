package crc6477f0d89a9cfd64b1;


public class OpenGLViewRenderer_Renderer
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		android.opengl.GLSurfaceView.Renderer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onDrawFrame:(Ljavax/microedition/khronos/opengles/GL10;)V:GetOnDrawFrame_Ljavax_microedition_khronos_opengles_GL10_Handler:Android.Opengl.GLSurfaceView/IRendererInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"n_onSurfaceChanged:(Ljavax/microedition/khronos/opengles/GL10;II)V:GetOnSurfaceChanged_Ljavax_microedition_khronos_opengles_GL10_IIHandler:Android.Opengl.GLSurfaceView/IRendererInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"n_onSurfaceCreated:(Ljavax/microedition/khronos/opengles/GL10;Ljavax/microedition/khronos/egl/EGLConfig;)V:GetOnSurfaceCreated_Ljavax_microedition_khronos_opengles_GL10_Ljavax_microedition_khronos_egl_EGLConfig_Handler:Android.Opengl.GLSurfaceView/IRendererInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Controls.Compatibility.Platform.Android.OpenGLViewRenderer+Renderer, Microsoft.Maui.Controls.Compatibility", OpenGLViewRenderer_Renderer.class, __md_methods);
	}


	public OpenGLViewRenderer_Renderer ()
	{
		super ();
		if (getClass () == OpenGLViewRenderer_Renderer.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Compatibility.Platform.Android.OpenGLViewRenderer+Renderer, Microsoft.Maui.Controls.Compatibility", "", this, new java.lang.Object[] {  });
	}


	public void onDrawFrame (javax.microedition.khronos.opengles.GL10 p0)
	{
		n_onDrawFrame (p0);
	}

	private native void n_onDrawFrame (javax.microedition.khronos.opengles.GL10 p0);


	public void onSurfaceChanged (javax.microedition.khronos.opengles.GL10 p0, int p1, int p2)
	{
		n_onSurfaceChanged (p0, p1, p2);
	}

	private native void n_onSurfaceChanged (javax.microedition.khronos.opengles.GL10 p0, int p1, int p2);


	public void onSurfaceCreated (javax.microedition.khronos.opengles.GL10 p0, javax.microedition.khronos.egl.EGLConfig p1)
	{
		n_onSurfaceCreated (p0, p1);
	}

	private native void n_onSurfaceCreated (javax.microedition.khronos.opengles.GL10 p0, javax.microedition.khronos.egl.EGLConfig p1);

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
