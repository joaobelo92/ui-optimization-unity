using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NetMQ;
using NetMQ.Sockets;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

public class client : MonoBehaviour
{
    Thread client_thread;
    Thread webcam_upload;
    WebCamTexture webcamTexture;
    Color32[] data;
    byte[] frame;
    AnchorsObject requestResult;
    RequestSocket requestSocket;
    bool sendWebcamFeed = false;

    private bool requestsCancelled;

    // Start is called before the first frame update
    void Start()
    {
        requestsCancelled = false;
        client_thread = new Thread(NetMQClient);
        client_thread.Start();

        if (sendWebcamFeed)
        {
            webcamTexture = new WebCamTexture(640, 480, 30);
            webcamTexture.Play();
            webcam_upload = new Thread(WebcamUpload);
            webcam_upload.Start();
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (sendWebcamFeed)
        {
            if (webcamTexture.width > 100)
            {
                if (data == null)
                {
                    data = new Color32[webcamTexture.width * webcamTexture.height];
                    webcamTexture.requestedFPS = 30;
                }

                webcamTexture.GetPixels32(data);
                frame = Color32ArrayToByteArray(data);

                print(Time.frameCount / Time.time);
            }
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            StartCoroutine(GetAnchors());
        }
    }

    private void OnDestroy()
    {
        requestsCancelled = true;
    }

    IEnumerator GetAnchors()
    {
        Thread requestThread = new Thread(RequestAnchors);
        requestThread.Start();
        yield return new WaitUntil(() => requestResult != null);
        List<GameObject> cubes = new List<GameObject>();
        foreach (Anchor anchor in requestResult.anchors)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            // opensim coordinate system
            obj.transform.position = new Vector3(anchor.z, anchor.y, anchor.x);
            obj.transform.localScale = new Vector3(5f, 5f, 5f);
        }
    }


    void RequestAnchors()
    {
        requestSocket.SendFrame("A");
        var msg = requestSocket.ReceiveFrameBytes();
        string msgString = System.Text.Encoding.UTF8.GetString(msg);
        requestResult = JsonUtility.FromJson<AnchorsObject>(msgString);
    }

    private static byte[] Color32ArrayToByteArray(Color32[] colors)
    {
        if (colors == null || colors.Length == 0)
            return null;

        int lengthOfColor32 = Marshal.SizeOf(typeof(Color32));
        int length = lengthOfColor32 * colors.Length;
        byte[] bytes = new byte[length];

        GCHandle handle = default(GCHandle);
        try
        {
            handle = GCHandle.Alloc(colors, GCHandleType.Pinned);
            IntPtr ptr = handle.AddrOfPinnedObject();
            Marshal.Copy(ptr, bytes, 0, length);
        }
        finally
        {
            if (handle != default(GCHandle))
                handle.Free();
        }

        return bytes;
    }

    void NetMQClient()
    {
        AsyncIO.ForceDotNet.Force();

        requestSocket = new RequestSocket();
        requestSocket.Connect("tcp://127.0.0.1:5555");

        while (!requestsCancelled)
        {

        }

        requestSocket.Close();
        NetMQConfig.Cleanup();

        //using (var reqSocket = new RequestSocket())
        //{
        //    reqSocket.Connect("tcp://127.0.0.1:5555");
        //    while (!requestsCancelled)
        //    {
        //        if (frame != null)
        //        {
        //            reqSocket.SendMoreFrame("F");
        //            string frameBase64 = System.Convert.ToBase64String(frame);
        //            reqSocket.SendFrame(frameBase64);

        //            var msg = reqSocket.ReceiveFrameString();
        //            print("From Server: " + msg);
        //        }
        //    }
        //    reqSocket.Close();
        //}
        //NetMQConfig.Cleanup();
    }

    void WebcamUpload()
    {
        while(true)
        {
            if (requestSocket != null && frame != null && !requestsCancelled)
            {
                requestSocket.SendMoreFrame("F");
                string frameBase64 = System.Convert.ToBase64String(frame);
                requestSocket.SendFrame(frameBase64);

                var msg = requestSocket.ReceiveFrameString();
                print("From Server: " + msg);
            }
        }
    }

    [Serializable]
    public class Anchor
    {
        public int id;
        public float x;
        public float y;
        public float z;

        public override string ToString()
        {
            return "x: " + x + " " + "y: " + y + " " + "z: " + z + " ";
        }
    }

    [Serializable]
    public class AnchorsObject
    {
        public List<Anchor> anchors;
    }

}
