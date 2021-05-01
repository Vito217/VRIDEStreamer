using System;
using System.Runtime.InteropServices;

public static class KeyboardModule
{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    const UInt32 WM_KEYDOWN = 0x0100;

    [DllImport("user32.dll")]
    static extern bool PostMessage(IntPtr hWnd, UInt32 Msg, int wParam, int lParam);

    public static void PressKey(IntPtr hwnd, int keycode)
    {
        PostMessage(hwnd, WM_KEYDOWN, keycode, 0);
    }
#endif
}
