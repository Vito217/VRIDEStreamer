using System.Net;
using System.Threading;
using UnityEngine;
using TMPro;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.IO;

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
    Thread thread3;

    List<IntPtr> currentRunningWindows;
    IDictionary<IntPtr, string> windows;

    void Start()
    {
        windows = WindowHandler.GetOpenWindows();
        width = Screen.currentResolution.width;
        height = Screen.currentResolution.height;
        currentRunningWindows = new List<IntPtr>();

        var host = Dns.GetHostEntry(Dns.GetHostName());
        IP = host.AddressList[host.AddressList.Length - 1].ToString();
        port = 5000;

        hostname.text = "host: " + IP;
        portnumber.text = "port: " + port;

        thread1 = new Thread(RequestsThread);
        thread2 = new Thread(DesktopThread);
        thread3 = new Thread(ClicksThread);
        thread1.Start();
        thread2.Start();
        thread3.Start();
    }

    void Update()
    {
        windows = WindowHandler.GetOpenWindows();
        foreach (IntPtr hwnd in windows.Keys)
            if (!currentRunningWindows.Contains(hwnd))
                WindowThread(hwnd);
    }

    private void OnApplicationQuit()
    {
        keepListening = false;
        thread1.Join();
        thread2.Join();
        thread3.Join();
    }

    static void EmulateMouseInteraction(string data)
    {
        string[] parameters = data.Split('.');
        if(parameters[0].Equals("desktop"))
        {
            int x = int.Parse(parameters[1]);
            int y = int.Parse(parameters[2]);
            MouseModule.SetCursorPosition(x, y);
            MouseModule.MouseClick();
        }
        else if (parameters[0].Equals("window"))
        {

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

    // ------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Sends screenshots from a window
    /// </summary>
    async void WindowThread(IntPtr hwnd)
    {
        currentRunningWindows.Add(hwnd);
        await Task.Run(() => 
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add("http://" + IP + ":" + port + "/" + hwnd.ToString() + "/");
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
                    byte[] buffer = WindowHandler.BitmapToArray(WindowHandler.PrintWindow(hwnd));
                    response.ContentLength64 = buffer.Length;
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                }
                finally
                {
                    response.OutputStream.Close();
                }
            }

            while (WindowHandler.IsWindow(hwnd) && keepListening)
            {
                var context = listener.BeginGetContext(new AsyncCallback(ListenerCallback), listener);
                context.AsyncWaitHandle.WaitOne(1, true);
            }

            listener.Close();
        }
        );
        currentRunningWindows.Remove(hwnd);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Recieves requests from the user. It returns a list with the names of the windows.
    /// Data will be the following:
    /// 
    ///     (desktop or window).(hwnd).(X Coord).(Y Coord)
    /// 
    /// </summary>
    void ClicksThread()
    {
        // Creating new listener
        HttpListener listener = new HttpListener();
        listener.Prefixes.Add("http://" + IP + ":" + port + "/click/");
        listener.Start();

        void RequestsCallback(IAsyncResult result)
        {
            HttpListener listener = (HttpListener)result.AsyncState;
            HttpListenerContext context = listener.EndGetContext(result);
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            try
            {
                /**
                string requestData;

                using (Stream body = request.InputStream)
                using (var reader = new StreamReader(body, request.ContentEncoding))
                    requestData = reader.ReadToEnd();

                if (!String.IsNullOrWhiteSpace(requestData))
                    EmulateMouseInteraction(requestData);
                **/
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
}
