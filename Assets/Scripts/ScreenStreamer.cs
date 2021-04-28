using System.Net;
using System.Threading;
using UnityEngine;
using TMPro;
using System;
using System.Collections.Generic;
using System.Text;

public class ScreenStreamer : MonoBehaviour
{
    bool keepListening = true;

    string IP;
    int width;
    int height;
    int port;

    public TextMeshProUGUI hostname;
    public TextMeshProUGUI portnumber;

    Thread thread1;
    Thread thread2;

    static IntPtr targetWindow;

    IDictionary<IntPtr, string> windows;

    void Start()
    {
        windows = WindowHandler.GetOpenWindows();
        width = Screen.currentResolution.width;
        height = Screen.currentResolution.height;

        var host = Dns.GetHostEntry(Dns.GetHostName());
        IP = host.AddressList[host.AddressList.Length - 1].ToString();
        port = 5000;

        hostname.text = "host: " + IP;
        portnumber.text = "port: " + port;

        thread1 = new Thread(RequestsThread);
        thread2 = new Thread(DesktopThread);
        thread1.Start();
        thread2.Start();
    }

    void Update()
    {
        windows = WindowHandler.GetOpenWindows();
    }

    private void OnApplicationQuit()
    {
        keepListening = false;
        thread1.Join();
        thread2.Join();
    }

    static byte[] CaptureWindow(IntPtr hwnd)
    {
        object bmap = WindowHandler.PrintWindow(hwnd);
        return WindowHandler.BitmapToArray(bmap);
    }

    static void EmulateMouseInteraction(string data)
    {
        string[] parameters = data.Split('.');
        if(parameters[0].Equals("True"))
        {
            int x = int.Parse(parameters[1]);
            int y = int.Parse(parameters[2]);
            MouseModule.SetCursorPosition(x, y);
            MouseModule.MouseClick();
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Recieves requests from the user. It returns a list with the names of the windows
    /// </summary>
    void RequestsThread()
    {
        // Creating new listener
        HttpListener listener = new HttpListener();
        listener.Prefixes.Add("http://" + IP + ":" + port + "/");
        listener.Start();

        void RequestsCallback(IAsyncResult result)
        {
            HttpListener listener = (HttpListener)result.AsyncState;
            HttpListenerContext context = listener.EndGetContext(result);
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            try
            {
                string data = "";
                foreach (KeyValuePair<IntPtr, string> pair in windows)
                    data += pair.Key + "=" + pair.Value + ";";

                byte[] buffer = Encoding.UTF8.GetBytes(data);
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            finally
            {
                response.OutputStream.Close();
            }
        }

        // Main loop
        while (keepListening)
        {
            var context = listener.BeginGetContext(new AsyncCallback(RequestsCallback), listener);
            context.AsyncWaitHandle.WaitOne(1, true);
        }

        listener.Close();
    }

    // ------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Sends screenshots from the entire desktop
    /// </summary>
    void DesktopThread()
    {
        object bmp = WindowHandler.NewBitmap(width, height);
        object g = WindowHandler.NewGraphics(bmp);
        object s = WindowHandler.NewSize(width, height);
        object[] copyParams = new object[] { 0, 0, 0, 0, s };

        HttpListener listener = new HttpListener();
        listener.Prefixes.Add("http://" + IP + ":" + port + "/desktop/");
        listener.Start();

        // Callback used for handling data
        void ListenerCallback(IAsyncResult result)
        {
            HttpListener listener = (HttpListener)result.AsyncState;
            HttpListenerContext context = listener.EndGetContext(result);
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            try
            {
                WindowHandler.CopyFromScreen(g, copyParams);
                byte[] buffer = WindowHandler.BitmapToArray(bmp);
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            finally
            {
                response.OutputStream.Close();
            }
        }

        while (keepListening)
        {
            var context = listener.BeginGetContext(new AsyncCallback(ListenerCallback), listener);
            context.AsyncWaitHandle.WaitOne(1, true);
        }

        WindowHandler.DisposeGraphics(g);
        listener.Close();
    }
}
