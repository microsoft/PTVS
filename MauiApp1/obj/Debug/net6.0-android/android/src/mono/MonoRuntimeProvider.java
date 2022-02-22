package mono;

//NOTE: we can't use import, see Generator.GetMonoInitSource

public class MonoRuntimeProvider
	extends android.content.ContentProvider
{
	public MonoRuntimeProvider ()
	{
	}

	@Override
	public boolean onCreate ()
	{
		return true;
	}

	@Override
	public void attachInfo (android.content.Context context, android.content.pm.ProviderInfo info)
	{
		// Mono Runtime Initialization {{{
		android.content.pm.ApplicationInfo applicationInfo = context.getApplicationInfo ();
		String[] apks = null;
		if (android.os.Build.VERSION.SDK_INT >= 21) {
			String[] splitApks = applicationInfo.splitPublicSourceDirs;
			if (splitApks != null && splitApks.length > 0) {
				apks = new String[splitApks.length + 1];
				apks [0] = applicationInfo.sourceDir;
				System.arraycopy (splitApks, 0, apks, 1, splitApks.length);
			}
		}
		if (apks == null) {
			apks = new String[] { applicationInfo.sourceDir };
		}
		mono.MonoPackageManager.LoadApplication (context, applicationInfo, apks);
		// }}}
		super.attachInfo (context, info);
	}

	@Override
	public android.database.Cursor query (android.net.Uri uri, String[] projection, String selection, String[] selectionArgs, String sortOrder)
	{
		throw new RuntimeException ("This operation is not supported.");
	}

	@Override
	public String getType (android.net.Uri uri)
	{
		throw new RuntimeException ("This operation is not supported.");
	}

	@Override
	public android.net.Uri insert (android.net.Uri uri, android.content.ContentValues initialValues)
	{
		throw new RuntimeException ("This operation is not supported.");
	}

	@Override
	public int delete (android.net.Uri uri, String where, String[] whereArgs)
	{
		throw new RuntimeException ("This operation is not supported.");
	}

	@Override
	public int update (android.net.Uri uri, android.content.ContentValues values, String where, String[] whereArgs)
	{
		throw new RuntimeException ("This operation is not supported.");
	}
}

