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
    Thread thread4;

    List<IntPtr> currentRunningWindows;
    IDictionary<IntPtr, string> windows;

    void Start()
    {
        width = Screen.currentResolution.width;
        height = Screen.currentResolution.height;
        currentRunningWindows = new List<IntPtr>();

        var host = Dns.GetHostEntry(Dns.GetHostName());
        IP = host.AddressList[host.AddressList.Length - 1].ToString();
        port = 5000;

        hostname.text = "host: " + IP;
        portnumber.text = "port: " + port;

        thread1 = new Thread(DesktopThread);
        thread1.Start();

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN

        windows = WindowHandler.GetOpenWindows();
        thread2 = new Thread(RequestsThread);
        thread2.Start();

        thread3 = new Thread(ClicksThread);
        thread3.Start();

        thread4 = new Thread(KeypressThread);
        thread4.Start();
#endif
    }

    void Update()
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        windows = WindowHandler.GetOpenWindows();
        foreach (IntPtr hwnd in windows.Keys)
            if (!currentRunningWindows.Contains(hwnd))
                WindowThread(hwnd);
#endif
    }

    private void OnApplicationQuit()
    {
        keepListening = false;
        thread1.Join();
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        thread2.Join();
        thread3.Join();
        thread4.Join();
# endif
    }

    // ------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Sends screenshots from the entire desktop
    /// </summary>
    void DesktopThread()
    {
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
                byte[] buffer = WindowHandler.PrintDesktop(width, height);
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

        listener.Close();
    }

    // ------------------------------------------------------------------------------------------------------------------------------------
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
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
                    byte[] buffer = WindowHandler.PrintWindow(hwnd);
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
    ///     (hwnd).(X Coord).(Y Coord)
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
                using Stream body = request.InputStream;
                using var reader = new StreamReader(body, request.ContentEncoding);
                string requestData = reader.ReadToEnd();
                string[] parameters = requestData.Split('.');

                string hwnd = parameters[0];
                int x = int.Parse(parameters[1]);
                int y = int.Parse(parameters[2]);

                if (hwnd.Equals("desktop"))
                    WindowHandler.ClickDesktop(x, y);
                else
                    WindowHandler.ClickWindow(new IntPtr(Convert.ToInt32(hwnd)), x, y);
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
    /// Recieves key presses from the user as:
    /// 
    ///     (hwnd).(keycode)
    /// 
    /// </summary>
    void KeypressThread()
    {
        // Creating new listener
        HttpListener listener = new HttpListener();
        listener.Prefixes.Add("http://" + IP + ":" + port + "/keypress/");
        listener.Start();

        void RequestsCallback(IAsyncResult result)
        {
            HttpListener listener = (HttpListener)result.AsyncState;
            HttpListenerContext context = listener.EndGetContext(result);
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            try
            {
                using Stream body = request.InputStream;
                using var reader = new StreamReader(body, request.ContentEncoding);
                string requestData = reader.ReadToEnd();
                string[] parameters = requestData.Split('.');

                string hwnd = parameters[0];
                int keycode = int.Parse(parameters[1]);

                if (hwnd != "desktop")
                    KeyboardModule.PressKey(new IntPtr(Convert.ToInt32(hwnd)), keycode);
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
#endif
}
