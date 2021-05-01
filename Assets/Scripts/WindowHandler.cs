using System;
using System.Collections.Generic;
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

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
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
    static extern bool ShowWindow(IntPtr hWnd, uint nCmdShow);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;        // x position of upper-left corner
        public int Top;         // y position of upper-left corner
        public int Right;       // x position of lower-right corner
        public int Bottom;      // y position of lower-right corner
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

    public static byte[] PrintWindow(IntPtr hwnd)
    {
        uint SW_RESTORE = 0x09;
        ShowWindow(hwnd, SW_RESTORE);

        RECT rc;
        GetWindowRect(hwnd, out rc);
        int width = rc.Right - rc.Left;
        int height = rc.Bottom - rc.Top;
        int pf = (int)pixelFormat.GetField("Format32bppArgb").GetValue(null);
        object bmp = NewBitmap(width, height, pf);
        object gfxBmp = NewGraphics(bmp);
        IntPtr hdcBitmap = GetHdcFromGraphics(gfxBmp);
        PrintWindow(hwnd, hdcBitmap, 0);
        ReleaseHdcFromGraphics(gfxBmp, hdcBitmap);
        DisposeGraphics(gfxBmp);

        return BitmapToArray(bmp);
    }

    public static void ClickWindow(IntPtr hwnd, int relXPos, int relYpos)
    {
        // First, we maximize window
        uint SW_RESTORE = 0x09;
        ShowWindow(hwnd, SW_RESTORE);

        // We set window to topmost position
        //IntPtr HWND_TOPMOST = new IntPtr(-1);
        IntPtr HWND_TOP = new IntPtr(0);
        const UInt32 SWP_NOSIZE = 0x0001;
        const UInt32 SWP_NOMOVE = 0x0002;
        const UInt32 SWP_SHOWWINDOW = 0x0040;
        SetWindowPos(hwnd, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);

        // We calculate the desktop screen position
        RECT rc;
        GetWindowRect(hwnd, out rc);
        int worldXpos = rc.Left + relXPos;
        int worldYPos = rc.Top + relYpos;

        ClickDesktop(worldXpos, worldYPos);
    }

    public static void ClickDesktop(int xPos, int yPos)
    {
        MouseModule.SetCursorPosition(xPos, yPos);
        MouseModule.MouseClick();
    }
#endif

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

    public static byte[] PrintDesktop(int width, int height)
    {
        object bmp = NewBitmap(width, height);
        object gfx = NewGraphics(bmp);
        object s = NewSize(width, height);
        copyFromScreen.Invoke(gfx, new object[] { 0, 0, 0, 0, s });
        DisposeGraphics(gfx);

        return BitmapToArray(bmp);
    }

    public static byte[] BitmapToArray(object bmp)
    {
        using (var stream = new MemoryStream())
        {
            SaveBitmap(bmp, stream, pngImageFormat);
            return stream.ToArray();
        }
    }
}