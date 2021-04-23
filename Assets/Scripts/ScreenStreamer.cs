using System.Net;
using System.IO;
using System.Threading;
using UnityEngine;
using TMPro;
using System;
using System.Reflection;
using System.Runtime.InteropServices;

public class ScreenStreamer : MonoBehaviour
{
    HttpListener listener;
    bool keepListening = true;

    string IP;
    int port = 5000;

    public TextMeshProUGUI hostname;
    public TextMeshProUGUI portnumber;

    static Assembly common;
    static Assembly primitives;
    static int width;
    static int height;
    static string path;

    Thread mainThread;

    void Start()
    {
        common = Assembly.Load("System.Drawing.Common");
        primitives = Assembly.Load("System.Drawing.Primitives");
        width = Screen.currentResolution.width;
        height = Screen.currentResolution.height;
        path = Path.Combine(Application.persistentDataPath, "temp_capture.png");

        var host = Dns.GetHostEntry(Dns.GetHostName());
        IP = host.AddressList[host.AddressList.Length - 1].ToString();

        hostname.text = "host: " + IP;
        portnumber.text = "port: " + port;

        listener = new HttpListener();
        listener.Prefixes.Add("http://" + IP + ":" + port + "/");
        listener.Start();

        mainThread = new Thread(Listen);
        mainThread.Start();
    }

    void Listen()
    {
        while (keepListening)
        {
            var context = listener.BeginGetContext(new AsyncCallback(ListenerCallback), listener);
            context.AsyncWaitHandle.WaitOne(1, true);
        }
    }

    static void ListenerCallback(IAsyncResult result)
    {
        HttpListener listener = (HttpListener)result.AsyncState;
        HttpListenerContext context = listener.EndGetContext(result);
        HttpListenerRequest request = context.Request;
        HttpListenerResponse response = context.Response;

        try
        {
            Capture();
            byte[] buffer = File.ReadAllBytes(path);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }
        catch
        {

        }
        finally
        {
            response.OutputStream.Close();
        }
    }

    private void OnApplicationQuit()
    {
        keepListening = false;
        mainThread.Join();
        listener.Close();
    }

    static void Capture()
    {
        Type size = primitives.GetType("System.Drawing.Size");
        Type bitmap = common.GetType("System.Drawing.Bitmap");
        Type graphics = common.GetType("System.Drawing.Graphics");

        ConstructorInfo bitmapConstructor = bitmap.GetConstructor(new Type[] { typeof(int), typeof(int) });
        object bmap = bitmapConstructor.Invoke(new object[] { width, height });

        object g = graphics.GetMethod("FromImage").Invoke(null, new object[] { bmap });

        ConstructorInfo sizeConstructor = size.GetConstructor(new Type[] { typeof(int), typeof(int) });
        object s = sizeConstructor.Invoke(new object[] { width, height });

        graphics.GetMethod("CopyFromScreen", new Type[] { typeof(int), typeof(int), typeof(int), typeof(int), size })
            .Invoke(g, new object[] { 0, 0, 0, 0, s });

        bitmap.GetMethod("Save", new Type[] { typeof(string) }).Invoke(bmap, new object[] { path });
    }

    [Flags]
    public enum MouseEventFlags
    {
        LeftDown = 0x00000002,
        LeftUp = 0x00000004,
        MiddleDown = 0x00000020,
        MiddleUp = 0x00000040,
        Move = 0x00000001,
        Absolute = 0x00008000,
        RightDown = 0x00000008,
        RightUp = 0x00000010
    }

    [DllImport("user32.dll", EntryPoint = "SetCursorPos")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out MousePoint lpMousePoint);

    [DllImport("user32.dll")]
    private static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);

    public static void SetCursorPosition(int x, int y)
    {
        SetCursorPos(x, y);
    }

    public static void SetCursorPosition(MousePoint point)
    {
        SetCursorPos(point.X, point.Y);
    }

    public static MousePoint GetCursorPosition()
    {
        MousePoint currentMousePoint;
        var gotPoint = GetCursorPos(out currentMousePoint);
        if (!gotPoint) { currentMousePoint = new MousePoint(0, 0); }
        return currentMousePoint;
    }

    public static void MouseEvent(MouseEventFlags value)
    {
        MousePoint position = GetCursorPosition();

        mouse_event
            ((int)value,
             position.X,
             position.Y,
             0,
             0)
            ;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MousePoint
    {
        public int X;
        public int Y;

        public MousePoint(int x, int y)
        {
            X = x;
            Y = y;
        }
    }
}
