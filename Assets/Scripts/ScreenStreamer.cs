using System.Net;
using System.IO;
using System.Threading;
using UnityEngine;
using TMPro;
using System;

public class ScreenStreamer : MonoBehaviour
{
    HttpListener listener;
    //bool keepListening = true;

    string IP;
    int port = 5000;

    public TextMeshProUGUI hostname;
    public TextMeshProUGUI portnumber;

    public static string path;

    Thread mainThread;

    void Start()
    {
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
        while (true)
        {
            HttpListenerContext context = listener.GetContext();
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            while (!IsFileReady(ScreenStreamer.path)) { }
            byte[] buffer = File.ReadAllBytes(ScreenStreamer.path);

            response.ContentLength64 = buffer.Length;
            Stream output = response.OutputStream;
            output.Write(buffer, 0, buffer.Length);
            output.Close();
        }
    }

    private void OnApplicationQuit()
    {
        mainThread.Abort();
        listener.Close();
    }

    public static bool IsFileReady(string filename)
    {
        // If the file can be opened for exclusive access it means that the file
        // is no longer locked by another process.
        try
        {
            using (FileStream inputStream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.None))
                return inputStream.Length > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
