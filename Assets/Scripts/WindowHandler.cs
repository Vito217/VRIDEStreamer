using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

public static class WindowHandler
{
    static Assembly common = Assembly.Load("System.Drawing.Common");
    static Assembly primitives = Assembly.Load("System.Drawing.Primitives");

    public static Type size = primitives.GetType("System.Drawing.Size");
    public static Type bitmap = common.GetType("System.Drawing.Bitmap");
    public static Type graphics = common.GetType("System.Drawing.Graphics");
    public static Type imageFormat = common.GetType("System.Drawing.Imaging.ImageFormat");
    public static Type pixelFormat = common.GetType("System.Drawing.Imaging.PixelFormat");

    static ConstructorInfo sizeConstructor = size.GetConstructor(new Type[] { typeof(int), typeof(int) });
    static ConstructorInfo bitmapConstructor1 = bitmap.GetConstructor(new Type[] { typeof(int), typeof(int) });
    static ConstructorInfo bitmapConstructor2 = bitmap.GetConstructor(new Type[] { typeof(int), typeof(int), pixelFormat });

    static MethodInfo fromImage = graphics.GetMethod("FromImage");
    static MethodInfo getHdc = graphics.GetMethod("GetHdc");
    static MethodInfo releaseHdc = graphics.GetMethod("ReleaseHdc", new Type[] { typeof(IntPtr) });
    static MethodInfo disposeGfx = graphics.GetMethod("Dispose");
    static MethodInfo copyFromScreen = graphics.GetMethod("CopyFromScreen", new Type[] { typeof(int), typeof(int), typeof(int), typeof(int), size });
    static MethodInfo saveBitmap = bitmap.GetMethod("Save", new Type[] { typeof(Stream), imageFormat });

    static object pngImageFormat = imageFormat.GetProperty("Png").GetValue(null);

    public enum ShowWindowCommands
    {
        SW_MAXIMIZE = 3,
        SW_MINIMIZE = 6
    }

    delegate bool EnumWindowsProc(IntPtr hWnd, int lParam);
    [DllImport("user32.dll")]
    static extern bool EnumWindows(EnumWindowsProc enumFunc, int lParam);
    [DllImport("user32.dll")]
    static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll")]
    static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")]
    static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")]
    static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")]
    static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, int nFlags);
    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool ShowWindow(IntPtr hWnd, ShowWindowCommands nCmdShow);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;        // x position of upper-left corner
        public int Top;         // y position of upper-left corner
        public int Right;       // x position of lower-right corner
        public int Bottom;      // y position of lower-right corner
    }

    public static object NewSize(int width, int height)
    {
        return sizeConstructor.Invoke(new object[] { width, height });
    }

    public static object NewBitmap(int width, int height)
    {
        return bitmapConstructor1.Invoke(new object[] { width, height });
    }

    public static object NewBitmap(int width, int height, object pf)
    {
        return bitmapConstructor2.Invoke(new object[] { width, height, pf });
    }

    public static object NewGraphics(object bmp)
    {
        return fromImage.Invoke(null, new object[] { bmp });
    }

    public static IntPtr GetHdcFromGraphics(object gfxBmp)
    {
        return (IntPtr)getHdc.Invoke(gfxBmp, null);
    }

    public static void ReleaseHdcFromGraphics(object gfxBmp, IntPtr hdcBitmap)
    {
        releaseHdc.Invoke(gfxBmp, new object[] { hdcBitmap });
    }

    public static void DisposeGraphics(object gfxBmp)
    {
        disposeGfx.Invoke(gfxBmp, null);
    }

    public static void SaveBitmap(object bmp, Stream stream, object imageFormat)
    {
        saveBitmap.Invoke(bmp, new object[] { stream, imageFormat });
    }

    public static void CopyFromScreen(object gfx, object[] parameters)
    {
        copyFromScreen.Invoke(gfx, parameters);
    }

    public static object PrintWindow(IntPtr hwnd)
    {
        RECT rc;
        GetWindowRect(hwnd, out rc);
        int width = rc.Right - rc.Left;
        int height = rc.Bottom - rc.Top;
        int pf = (int) pixelFormat.GetField("Format32bppArgb").GetValue(null);
        object bmp = NewBitmap(width, height, pf);
        object gfxBmp = NewGraphics(bmp);
        IntPtr hdcBitmap = GetHdcFromGraphics(gfxBmp);
        PrintWindow(hwnd, hdcBitmap, 0);
        ReleaseHdcFromGraphics(gfxBmp, hdcBitmap);
        DisposeGraphics(gfxBmp);

        return bmp;
    }

    public static byte[] BitmapToArray(object bmp)
    {
        using (var stream = new MemoryStream())
        {
            SaveBitmap(bmp, stream, pngImageFormat);
            return stream.ToArray();
        }
    }

    public static IntPtr WinGetHandle(string wName)
    {
        foreach (Process pList in Process.GetProcesses())
            if (pList.MainWindowTitle.Contains(wName))
                return pList.MainWindowHandle;

        return IntPtr.Zero;
    }

    public static string GetActiveWindow()
    {
        const int nChars = 256;
        IntPtr handle;
        StringBuilder Buff = new StringBuilder(nChars);
        handle = GetForegroundWindow();
        if (GetWindowText(handle, Buff, nChars) > 0)
        {
            return Buff.ToString();
        }
        return "";
    }

    public static IDictionary<IntPtr, string> GetOpenWindows()
    {
        IntPtr shellWindow = GetShellWindow();
        Dictionary<IntPtr, string> windows = new Dictionary<IntPtr, string>();

        EnumWindows(delegate (IntPtr hWnd, int lParam)
        {
            if (hWnd == shellWindow) return true;
            if (!IsWindowVisible(hWnd)) return true;

            int length = GetWindowTextLength(hWnd);
            if (length == 0) return true;

            StringBuilder builder = new StringBuilder(length);
            GetWindowText(hWnd, builder, length + 1);

            windows[hWnd] = builder.ToString();
            return true;

        }, 0);

        return windows;
    }

    public static void ExecuteWindowCommand(IntPtr hwnd, ShowWindowCommands cmd)
    {
        ShowWindow(hwnd, cmd);
    }
}