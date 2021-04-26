using System.Net;
using System.IO;
using System.Threading;
using UnityEngine;
using TMPro;
using System;
using System.Reflection;

public class ScreenStreamer : MonoBehaviour
{
    HttpListener listener;
    bool keepListening = true;

    string IP;
    int port = 5000;

    public TextMeshProUGUI hostname;
    public TextMeshProUGUI portnumber;

    Thread mainThread;

    static object imgf;
    static object bmp;
    static object g;

    static object[] copyParams;

    static MethodInfo copyFromScreen;
    static MethodInfo save;

    void Start()
    {
        Assembly common = Assembly.Load("System.Drawing.Common");
        Assembly primitives = Assembly.Load("System.Drawing.Primitives");
        Type size = primitives.GetType("System.Drawing.Size");
        Type bitmap = common.GetType("System.Drawing.Bitmap");
        Type graphics = common.GetType("System.Drawing.Graphics");
        Type imageFormat = common.GetType("System.Drawing.Imaging.ImageFormat");
        int width = Screen.currentResolution.width;
        int height = Screen.currentResolution.height;

        object s = size.GetConstructor(new Type[] { typeof(int), typeof(int) }).Invoke(new object[] { width, height });
        bmp = bitmap.GetConstructor(new Type[] { typeof(int), typeof(int) }).Invoke(new object[] { width, height });
        g = graphics.GetMethod("FromImage").Invoke(null, new object[] { bmp });
        imgf = imageFormat.GetProperty("Png").GetValue(null);

        copyFromScreen = graphics.GetMethod("CopyFromScreen", new Type[] { typeof(int), typeof(int), typeof(int), typeof(int), size });
        save = bitmap.GetMethod("Save", new Type[] { typeof(Stream), imageFormat });

        copyParams = new object[] { 0, 0, 0, 0, s };

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
            string requestData;

            using (Stream body = request.InputStream)
                using (var reader = new StreamReader(body, request.ContentEncoding))
                    requestData = reader.ReadToEnd();

            if (!String.IsNullOrWhiteSpace(requestData))
                EmulateMouseInteraction(requestData);

            byte[] buffer = Capture();
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }
        catch (Exception e)
        {
            Debug.Log(e.Message);
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

    static byte[] Capture()
    {
        copyFromScreen.Invoke(g, copyParams);
        using (var stream = new MemoryStream())
        {
            save.Invoke(bmp, new object[] { stream, imgf });
            return stream.ToArray();
        }
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
}
