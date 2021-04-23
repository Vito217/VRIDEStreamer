using System;
using System.IO;
using System.Threading;
using UnityEngine;
using System.Reflection;

public class ScreenCapture : MonoBehaviour
{
    Thread mainThread;
    int width;
    int height;
    Assembly common;
    Assembly primitives;


    private void Start()
    {

        //common = Assembly.LoadFrom(Path.Combine(Application.streamingAssetsPath, "System.Drawing.Common.dll"));
        //primitives = Assembly.LoadFrom(Path.Combine(Application.streamingAssetsPath, "System.Drawing.Primitives.dll"));

        common = Assembly.Load("System.Drawing.Common");
        primitives = Assembly.Load("System.Drawing.Primitives");

        width = Screen.currentResolution.width;
        height = Screen.currentResolution.height;
        mainThread = new Thread(Capture);
        mainThread.Start();
    }

    private void Update()
    {
        try
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.LoadImage(File.ReadAllBytes(ScreenStreamer.path));

            // Updating image
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(width * .5f, height * .5f));
            GameObject.Find("Canvas/Image").GetComponent<UnityEngine.UI.Image>().sprite = sprite;
        }
        catch
        {

        }
        
    }

    void Capture()
    {
        while (true)
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

            while (!IsFileReady(ScreenStreamer.path)) { }

            bitmap.GetMethod("Save", new Type[] { typeof(string) }).Invoke(bmap, new object[] { ScreenStreamer.path });
        }
    }

    private void OnApplicationQuit()
    {
        mainThread.Abort();
    }

    public static bool IsFileReady(string filename)
    {
        // If the file can be opened for exclusive access it means that the file
        // is no longer locked by another process.
        try
        {
            using (FileStream inputStream = File.Open(filename, FileMode.Open, FileAccess.Write, FileShare.None))
                return inputStream.Length > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
