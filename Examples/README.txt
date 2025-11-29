
--- 
For Python Native debugging example error python311_d.dll not found

## Quick fix (recommended): put the DLL on the exe’s PATH for Debug|x64

Use the VS UI (simpler than editing XML):

1. Right-click the **PythonNative** project → **Properties**.

2. Top left: **Configuration** = `Debug`, **Platform** = `x64`.

3. Go to **Configuration Properties → Debugging**.

4. In **Environment**, set: to your python311_d.dll location

   ```text
   PATH=C:\Users\bschnurr\AppData\Local\Programs\Python\Python313;%PATH%
   ```

5. Make sure **Debugger to launch** is whatever you’re using (`Python/Native Debugging` or `Local Windows Debugger`).

6. Apply, OK, rebuild, F5.

Now when VS starts `PythonNative.exe`, its PATH includes the folder where `python313_d.dll` lives, so the loader should find it and that specific error should go away.

-