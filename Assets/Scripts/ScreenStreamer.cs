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
    public static Dictionary<string, int> winCharKeycodes = new Dictionary<string, int>() {
        { "a", 0x41 }, { "b", 0x42 }, { "c", 0x43 }, { "d", 0x44 }, { "e", 0x45 },
        { "f", 0x46 }, { "g", 0x47 }, { "h", 0x48 }, { "i", 0x49 }, { "j", 0x4A },
        { "k", 0x4B }, { "l", 0x4C }, { "m", 0x4D }, { "n", 0x4E }, { "o", 0x4F },
        { "p", 0x50 }, { "q", 0x51 }, { "r", 0x52 }, { "s", 0x53 }, { "t", 0x54 },
        { "u", 0x55 }, { "v", 0x56 }, { "w", 0x57 }, { "x", 0x58 }, { "y", 0x59 },
        { "z", 0x5A },

        { "A", 0x41 | 0xA1 }, { "B", 0x42 | 0xA1 }, { "C", 0x43 | 0xA1 }, { "D", 0x44 | 0xA1 }, { "E", 0x45 | 0xA1 },
        { "F", 0x46 | 0xA1 }, { "G", 0x47 | 0xA1 }, { "H", 0x48 | 0xA1 }, { "I", 0x49 | 0xA1 }, { "J", 0x4A | 0xA1 },
        { "K", 0x4B | 0xA1 }, { "L", 0x4C | 0xA1 }, { "M", 0x4D | 0xA1 }, { "N", 0x4E | 0xA1 }, { "O", 0x4F | 0xA1 },
        { "P", 0x50 | 0xA1 }, { "Q", 0x51 | 0xA1 }, { "R", 0x52 | 0xA1 }, { "S", 0x53 | 0xA1 }, { "T", 0x54 | 0xA1 },
        { "U", 0x55 | 0xA1 }, { "V", 0x56 | 0xA1 }, { "W", 0x57 | 0xA1 }, { "X", 0x58 | 0xA1 }, { "Y", 0x59 | 0xA1 },
        { "Z", 0x5A | 0xA1 },
    };

    public static Dictionary<string, int> winNumKeycodes = new Dictionary<string, int>() {
        { "0", 0x30 }, { "1", 0x31 }, { "2", 0x32 }, { "3", 0x33 }, { "4", 0x34 },
        { "5", 0x35 }, { "6", 0x36 }, { "7", 0x37 }, { "8", 0x38 }, { "9", 0x39 },
    };

    public static Dictionary<string, int> winSpecialKeycodes = new Dictionary<string, int>() {
        { "enter", 0x0D }, { "ENTER", 0x0D },
        { "back", 0x08 }, { "BACK", 0x08 },
    };

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
    ///     (hwnd).(character)
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
                string character = parameters[1];

                if (hwnd != "desktop")
                {
                    // Case 1: this is an alphabetical character
                    if (winCharKeycodes.ContainsKey(character))
                    {
                        int keycode = winCharKeycodes[character];
                        KeyboardModule.PressKey(new IntPtr(Convert.ToInt32(hwnd)), keycode);
                    }

                    else if (winNumKeycodes.ContainsKey(character))
                    {
                        int keycode = winNumKeycodes[character];
                        KeyboardModule.PressKey(new IntPtr(Convert.ToInt32(hwnd)), keycode);
                    }

                    else if (winSpecialKeycodes.ContainsKey(character))
                    {
                        int keycode = winSpecialKeycodes[character];
                        KeyboardModule.PressKey(new IntPtr(Convert.ToInt32(hwnd)), keycode);
                    }
                }
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
