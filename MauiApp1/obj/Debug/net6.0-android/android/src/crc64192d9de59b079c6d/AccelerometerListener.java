package crc64192d9de59b079c6d;


public class AccelerometerListener
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		android.hardware.SensorEventListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onAccuracyChanged:(Landroid/hardware/Sensor;I)V:GetOnAccuracyChanged_Landroid_hardware_Sensor_IHandler:Android.Hardware.ISensorEventListenerInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"n_onSensorChanged:(Landroid/hardware/SensorEvent;)V:GetOnSensorChanged_Landroid_hardware_SensorEvent_Handler:Android.Hardware.ISensorEventListenerInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Essentials.AccelerometerListener, Microsoft.Maui.Essentials", AccelerometerListener.class, __md_methods);
	}


	public AccelerometerListener ()
	{
		super ();
		if (getClass () == AccelerometerListener.class)
			mono.android.TypeManager.Activate ("Microsoft.Maui.Essentials.AccelerometerListener, Microsoft.Maui.Essentials", "", this, new java.lang.Object[] {  });
	}


	public void onAccuracyChanged (android.hardware.Sensor p0, int p1)
	{
		n_onAccuracyChanged (p0, p1);
	}

	private native void n_onAccuracyChanged (android.hardware.Sensor p0, int p1);


	public void onSensorChanged (android.hardware.SensorEvent p0)
	{
		n_onSensorChanged (p0);
	}

	private native void n_onSensorChanged (android.hardware.SensorEvent p0);

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
