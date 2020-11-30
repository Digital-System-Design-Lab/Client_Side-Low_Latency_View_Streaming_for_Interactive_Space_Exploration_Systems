using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using System.Threading.Tasks;
using ISESStructure;
using System.Runtime.InteropServices;
using UnityEditor;
using ISESClient;
using ISESESAS;
using ISESProfiler;
using Born2Code.Net;
using System.Diagnostics;

public class Client_driver : MonoBehaviour
{
    

    #region Network variables
    [Header("Network Information")]
    public int Pos_port = 12000;
    public string Texture_server_IP = "192.168.0.2";
    int BANDWIDTH = 50;
    int Mega = 100000;

    //private string Texture_server_IP = "165.246.39.163";
    //private string Texture_server_IP = "116.34.184.61";
    [Header("Server to Client port number")]
    public int Texture_port = 11000;

    [Header("Must be the same in sender and receiver")]
    public int messageByteLength = 24;
    byte[] frameBytesLength;

    TcpClient posClient = null;
    TcpClient texClient = null;
    TcpListener posServer = null;

    NetworkStream pos_stream = null;
    NetworkStream tex_stream = null;

    byte[] posBytes;
    byte[] infoBytes;
    #endregion

    #region Positional Data
    ISESESAS.Path cur_path;
    Pos cur_pos;
    Loc cur_loc;
    Loc prev_loc;
    out_cache_search cur_result;
    DataPacket requestPacket;

    Request packet;
    Client_info client_info;
    #endregion

    #region 기타 변수들
    int time = 0;
    int sync = 0;
    public List<SubRange> range;
    Point2D[] predict_p;
    Velocity velo;

    int prev_seg_x;
    int prev_seg_y;

    bool skip = false;
    int receive_num;

    double e2eDelay = 0.0f;
    Stopwatch sw;
    Stopwatch sw1;
    #endregion

    #region 수행시간 측정 변수들
    DateTime end2end;


    #endregion

    #region 클래스 변수들
    Client client;
    Cacheinfo cacheinfo;
    ESASinfo esasinfo;
    viewinfo view_info;
    ISESESAS.Path path;
    Profiler profiler;
    Qualitylist misslist;
    #endregion

    #region Byte 변수
    byte[] framebuffer;
    byte[] jpegBytes;
    byte[] Lsubview;
    byte[] Fsubview;
    byte[] Rsubview;
    byte[] Bsubview;
    #endregion

    #region Thread 관련 변수
    Thread partial_rendering;
    Thread receiveThread;

    Thread[] receiveThreads;
    Thread renderingThread;

    object[] receiveLocks;
    object[] partialLocks;
    object receivelock;
    object renderlock;
    #endregion

    #region Unity 변수
    public GameObject Render_tex = null;
    public GameObject Equi = null;
    Texture2D tex;
    #endregion
    // Start is called before the first frame update
    void Start()
    {
        Application.runInBackground = true;
        init_ClassandStruct();
        init_Unity();
        init_Thread();
        EstablishConn();
        //shareClientInfo();
        sw = new Stopwatch();
        sw1 = new Stopwatch();
    }

    public void shareClientInfo()
    {
        frameBytesLength = new byte[messageByteLength];
        infoBytes = StructToBytes(client_info);

        byteLengthToFrameByteArray(infoBytes.Length, frameBytesLength);
        posClient.NoDelay = true;
        posClient.SendBufferSize = frameBytesLength.Length;
        NetworkStream pos_stream = posClient.GetStream();
        pos_stream.Write(frameBytesLength, 0, frameBytesLength.Length);
        posClient.NoDelay = true;
        posClient.SendBufferSize = infoBytes.Length;
        NetworkStream pos_stream1 = posClient.GetStream();
        pos_stream1.Write(infoBytes, 0, infoBytes.Length);
    }

    // Update is called once per frame
    void Update()
    {
#if false
        #region Request a view
        DateTime Start = DateTime.Now;
        getPos(time);
        packetize();
        if(!_isSameSeg() && cur_result.getStat() != CacheStatus.HIT)
        {
            send_Packet();
            skip = true;
            #region Receive a view
            if (cur_result.getStat() == CacheStatus.PARTIAL_HIT)
            {
                partialRendering();
            }
            receive_subseg();
        #endregion
        }
        #endregion
        
        #region Rendering(decode&up-sampling -> apply texture)
        if (skip)
        {
            tex.Apply();
            skip = false;
        }
        else
        {
            sw1.Start();
            Renderings();
            sw1.Stop();
            UnityEngine.Debug.LogFormat("Rendering time : {0}", sw1.ElapsedMilliseconds);
            sw1.Reset();
        }
        #endregion
        e2eDelay += (DateTime.Now - Start).TotalMilliseconds;
        if (time == client.pathlist.Length)
        {
            EditorApplication.isPlaying = false;
        }
        time++;
        client.cache.recordPrevPacket(packet);
        prev_loc = cur_loc;
#endif
    }

    void waitThreads()
    {
        for(int i = 0; i < receiveLocks.Length; i++)
        {
            receiveThreads[i].Join();
        }
        //receiveThread.Join();
        UnityEngine.Debug.LogError("Wait for receiveThreads");
    }

    void init_Thread()
    {
        receiveThreads = new Thread[client.cache.cacheinfo.seg_size];
        receiveLocks = new object[client.cache.cacheinfo.seg_size];
        partialLocks = new object[client.cache.cacheinfo.seg_size];
        for (int i = 0; i < client.cache.cacheinfo.seg_size; i++)
        {
            object temp = new object();
            object temp1 = new object();
            receiveLocks[i] = temp;
            partialLocks[i] = temp1;
        }
        receivelock = new object();
        renderlock = new object();
    }
    void init_ClassandStruct()
    {
        receive_num = 0;
        int one_way_length =480;
        double percentage = 0.5f;
        int cachesize = (int)((double)(one_way_length) * percentage);
        client = new Client();
        profiler = new Profiler();
        view_info = new viewinfo(2048, 4096, 3);
        int seg_size = 30;
        cacheinfo = new Cacheinfo(cachesize, seg_size, 720, 6720, Policy.LRU, 10);
        esasinfo = new ESASinfo(6, 0.3f, 0.6f);
        client.init(cacheinfo, esasinfo, view_info);
        client.read_path("Simple_circle.txt");
        framebuffer = new byte[view_info.width * view_info.height * view_info.bpp];
        range = calcSubrange(seg_size);
        cur_result = new out_cache_search();
        client_info = new Client_info(seg_size, client.pathlist.Length);
        prev_seg_x = -1;
        prev_seg_y = -1;
        
    }
    void init_Unity()
    {
        Render_tex = GameObject.Find("LFSpace");
        Equi = GameObject.Find("Equi");
        Render_tex.GetComponent<Renderer>().enabled = true;
        Equi.GetComponent<Renderer>().enabled = true;
        Render_tex.GetComponent<Renderer>().material.shader = Shader.Find("Unlit/Pano360Shader");
        Equi.GetComponent<Renderer>().material.shader = Shader.Find("Unlit/Pano360Shader");
        tex = new Texture2D(view_info.width, view_info.height, TextureFormat.RGB24, false);
        Render_tex.GetComponent<Renderer>().material.mainTexture = tex;
        Equi.GetComponent<Renderer>().material.mainTexture = tex;
    }
    public void dodelay(float target_delay)
    {
        //DateTime temp = DateTime.Now;
        //float excution_time = 0.0f;
        //while (target_delay > excution_time)
        //{
        //    excution_time = (DateTime.Now - temp).Milliseconds;
        //}
        Thread.Sleep((int)target_delay);
        //Task.Delay((int)target_delay);
        //UnityEngine.Debug.LogWarningFormat("Delay time : {0:f3}", excution_time);
    }
    public void dodelay(int target_delay)
    {
        //DateTime temp = DateTime.Now;
        //float excution_time = 0.0f;
        //while (target_delay > excution_time)
        //{
        //    excution_time = (DateTime.Now - temp).Milliseconds;
        //}
        Task.Delay((int)target_delay);
        //UnityEngine.Debug.LogWarningFormat("Delay time : {0:f3}", excution_time);
    }
    public void letidlestat(float target_delay)
    {
        DateTime temp = DateTime.Now;
        float excution_time = 0.0f;
        while (target_delay > excution_time)
        {
            excution_time = (DateTime.Now - temp).Milliseconds;
        }
        //UnityEngine.Debug.LogWarningFormat("Delay time : {0:f3}", excution_time);
    }
    public bool _isSameSeg()
    {
        if ((prev_seg_x == cur_loc.get_seg_pos().seg_pos_x) && (prev_seg_y == cur_loc.get_seg_pos().seg_pos_y))
        {
            return true;
        }
        else
        {
            prev_seg_x = cur_loc.get_seg_pos().seg_pos_x;
            prev_seg_y = cur_loc.get_seg_pos().seg_pos_y;

            return false;
        }
    }
    void getPos(int time)
    {
        cur_path = client.getPath(time);
    }
    void packetize()
    {
        int hd = client.getHeadDirection(cur_path.hd);
        int esl = client.calcESL(cur_path);
        esl = 0;
        string reqlist = client.getReqViewlist(esl, hd);
        cur_pos = new Pos(cur_path.x, cur_path.y, hd, esl);
        cur_loc = TransP2L();
        
        client.CacheTableUpdate(packet, cur_loc, reqlist, time, ref cur_result);
        misslist.convertSTR2LIST(cur_result.getMisslist());
        packet = new Request(cur_pos, misslist, cur_loc.get_seg_pos());

        if (client.cache.cacheinfo.policy == Policy.DR)
        {
            if (time == 0)
            {
                velo = client.cache.calcVelocity(packet, cur_loc);
            }
            else
            {
                velo = client.cache.calcVelocity(packet, prev_loc);

            }
            prediction();
        }
    }
    public Loc TransP2L()
    {
        client.myVR.classify_Location(cur_pos.getX(), cur_pos.getY());
        string region = client.myVR.getCurregion();
        int i = 0;
        int iter = 0;
        int Pos_x = client.myVR.getPos_X(); int Pos_y = client.myVR.getPos_Y();
        int start_x = 0; int end_x = 0;
        int start_y = 0; int end_y = 0;


        for (i = 0; i < range.Count; i++)
        {
            if ((Pos_x >= range[i]._start) && (Pos_x <= range[i]._end) && region.Substring(0, 1).Equals("R"))
            {
                start_x = range[i]._start;
                end_x = range[i]._end;
                iter = Pos_x - start_x;
            }
            if ((Pos_y >= range[i]._start) && (Pos_y <= range[i]._end) && region.Substring(0, 1).Equals("C"))
            {
                start_y = range[i]._start;
                end_y = range[i]._end;
                iter = Pos_y - start_y;
            }
        }
        //cache.cacheinfo.cachesize;
        sub_segment_pos seg_pos = new sub_segment_pos(start_x, end_x, start_y, end_y, client.cache.cacheinfo.seg_size);
        seg_pos.calcSeg_pos(client.cache.cacheinfo.seg_size, client.myVR.getCurregion().Substring(0, 1), cur_pos.getX(), cur_pos.getY(), client.myVR.getOrigin_X(), client.myVR.getOrigin_Y());

        Loc loc = new Loc(client.myVR.getCurregion(), seg_pos, iter);
        loc.setPos_X(Pos_x); loc.setPos_Y(Pos_y);

        return loc;
    }

    public void prediction()
    {
        bool isPredict = true;
        bool isPartial = false;
        if(time!=0 && time!=(client.pathlist.Length - 1))
        {
            if (cur_pos.getHead_dir() == 1 || cur_pos.getHead_dir() == 2)
            {
                if (cur_loc.iter == cacheinfo.seg_size - 1)
                {
                    //prediction을 한다.
                    predict_p = client.cache.predictPos(packet, velo);
                    UnityEngine.Debug.LogWarningFormat("{0} {1} {2} {3} predicted position", predict_p[0].getX(), predict_p[0].getY(),
                        predict_p[1].getX(), predict_p[1].getY());
                }
                else if (cur_loc.iter == 0)
                {
                    isPredict = false;
                    for (int i = 0; i < 10; i++)
                    {
                        if (predict_p[i].getX() == packet.pos.getX() && predict_p[i].getY() == packet.pos.getY())
                        {
                            //prediction 성공
                            isPredict = true;
                            break;
                        }
                    }
                }
            }
            else if (cur_pos.getHead_dir() == 0 || cur_pos.getHead_dir() == 3)
            {
                if (cur_loc.iter == 0)
                {
                    predict_p = client.cache.predictPos(packet, velo);
                    UnityEngine.Debug.LogWarningFormat("{0} {1} {2} {3} predicted position", predict_p[0].getX(), predict_p[0].getY(),
                        predict_p[1].getX(), predict_p[1].getY());
                }
                else if (cur_loc.iter == cacheinfo.seg_size - 1)
                {
                    for (int i = 0; i < 10; i++)
                    {
                        isPredict = false;
                        if (predict_p[i].getX() == packet.pos.getX() && predict_p[i].getY() == packet.pos.getY())
                        {
                            //prediction 성공
                            isPredict = true;
                            break;
                        }
                    }
                }
            }

            if (!isPredict)
            {
                UnityEngine.Debug.LogError("Prediction 실패!");
            }
        }
    }
    void send_Packet()
    {
        frameBytesLength = new byte[messageByteLength];
        posBytes = packet.StructToBytes(packet);

        byteLengthToFrameByteArray(posBytes.Length, frameBytesLength);
        posClient.NoDelay = true;   
        posClient.SendBufferSize = frameBytesLength.Length;
        NetworkStream pos_stream = posClient.GetStream();
        pos_stream.Write(frameBytesLength, 0, frameBytesLength.Length);
        posClient.NoDelay = true;
        posClient.SendBufferSize = posBytes.Length;
        NetworkStream pos_stream1 = posClient.GetStream();
        pos_stream1.Write(posBytes, 0, posBytes.Length);
    }
    void receive_view()
    {
        int viewsize = readPosByteSize(messageByteLength);
        readFrameByteArray(viewsize);
    }
    void receive_views()
    {
        if (packet.misslist.Left != QUALITY.EMPTY)
        {
            int viewsize = readPosByteSize(messageByteLength);
            readFrameByteArray(viewsize);
            Lsubview = jpegBytes;
        }
        if (packet.misslist.Front != QUALITY.EMPTY)
        {
            int viewsize = readPosByteSize(messageByteLength);
            readFrameByteArray(viewsize);
            Fsubview = jpegBytes;
        }
        if (packet.misslist.Right != QUALITY.EMPTY)
        {
            int viewsize = readPosByteSize(messageByteLength);
            readFrameByteArray(viewsize);
            Rsubview = jpegBytes;
        }
        if (packet.misslist.Back != QUALITY.EMPTY)
        {
            int viewsize = readPosByteSize(messageByteLength);
            readFrameByteArray(viewsize);
            Bsubview = jpegBytes;
        }
    }
#if false
    #region backUp
    void receive_subseg()
    {
        int idx = cur_result.getIdx();
        receiveTask = new Thread(() =>
        {
            receive_num = 0;
            Request _packet = packet;
            for (int i = 0; i < client.cache.cacheinfo.seg_size; i++)
            {
                DateTime start = DateTime.Now;
                if (_packet.misslist.Left != QUALITY.EMPTY)
                {
                    int viewsize = readPosByteSize(messageByteLength);
                    readFrameLeft(viewsize);
                    client.cache.table[idx].setlView(i, Lsubview);
                }
                if (_packet.misslist.Front != QUALITY.EMPTY)
                {
                    int viewsize = readPosByteSize(messageByteLength);
                    readFrameFront(viewsize);
                    client.cache.table[idx].setfView(i, Fsubview);
                    UnityEngine.Debug.Log(viewsize + " is viewsize");
                }
                if (_packet.misslist.Right != QUALITY.EMPTY)
                {
                    int viewsize = readPosByteSize(messageByteLength);
                    readFrameRight(viewsize);
                    client.cache.table[idx].setrView(i, Rsubview);
                }
                if (_packet.misslist.Back != QUALITY.EMPTY)
                {
                    int viewsize = readPosByteSize(messageByteLength);
                    readFrameBack(viewsize);
                    client.cache.table[idx].setbView(i, Bsubview);
                }
                receive_num += 1;
                UnityEngine.Debug.LogFormat("{0} 번째 view 수신 완료 {1:f3}  misslist : {2}", i, (DateTime.Now - start).TotalMilliseconds, _packet.misslist.Front.ToString());
            }
        });
        receiveTask.Start();
    }

    #endregion
#endif
    void RenderingView(int iter, int index)
    {
        int temp = iter;
        int idx = index;
        if (cur_result.getStat() != CacheStatus.PARTIAL_HIT)
        {
            lock (receiveLocks[temp])
            {
                client.rendersubviews(client.cache.table[idx].getlview(temp),
                    client.cache.table[idx].getfview(temp),
                    client.cache.table[idx].getrview(temp),
                    client.cache.table[idx].getbview(temp),
                    cur_result.getRenderlist());
            }
        }
        else
        {
            lock (partialLocks[temp])
            {
                client.renderPartialsubviews(client.cache.table[idx].getlview(temp),
                    client.cache.table[idx].getfview(temp),
                    client.cache.table[idx].getrview(temp),
                    client.cache.table[idx].getbview(temp),
                    cur_result.getMisslist(),
                    cur_result.getHitlist(),
                    temp);
            }
        }
    }
    void receive_subseg()
    {
        int index = cur_result.getIdx();
        Request _packet = packet;

        bool reverse = false;
        UnityEngine.Debug.LogWarning("iter " + cur_loc.iter);
        if (cur_loc.iter == 0)
        {
            reverse = false;
        }
        else if(cur_loc.iter == client.cache.cacheinfo.seg_size - 1)
        {
            reverse = true;
        }
        if (!reverse)
        {
            for (int i = 0; i < receiveThreads.Length; i++)
            {
                int iter = i;
                receiveThreads[iter] = new Thread(() => ReceiveView(_packet, index, iter));
                receiveThreads[iter].IsBackground = true;
                receiveThreads[iter].Start();
            }
        }
        else
        {
            for (int i = receiveThreads.Length-1; i >= 0; i--)
            {
                int iter = i;
                receiveThreads[iter] = new Thread(() => ReceiveView(_packet, index, iter));
                receiveThreads[iter].IsBackground = true;
                receiveThreads[iter].Start();
            }
        }
    }
    
    void ReceiveView(Request _packet, int idx, int i)
    {
        lock (receiveLocks[i])
        {
            lock (receivelock)
            {
                int total = 0;
                sw.Start();
                int num = readPosByteSize(messageByteLength);
                if (_packet.misslist.Left != QUALITY.EMPTY)
                {
                    int viewsize = readPosByteSize(messageByteLength);
                    total += viewsize;
                    readFrameLeft(viewsize);
                    client.cache.table[idx].setlView(num, Lsubview);
                }
                if (_packet.misslist.Front != QUALITY.EMPTY)
                {
                    int viewsize = readPosByteSize(messageByteLength);
                    total += viewsize;
                    readFrameFront(viewsize);
                    client.cache.table[idx].setfView(num, Fsubview);
                }
                if (_packet.misslist.Right != QUALITY.EMPTY)
                {
                    int viewsize = readPosByteSize(messageByteLength);
                    total += viewsize;
                    readFrameRight(viewsize);
                    client.cache.table[idx].setrView(num, Rsubview);
                }
                if (_packet.misslist.Back != QUALITY.EMPTY)
                {
                    int viewsize = readPosByteSize(messageByteLength);
                    total += viewsize;
                    readFrameBack(viewsize);
                    client.cache.table[idx].setbView(num, Bsubview);
                }
                sw.Stop();
                UnityEngine.Debug.LogWarningFormat("Receive view time : {0},  {1}", sw.ElapsedMilliseconds,total);
                sw.Reset();
            }
        }
    }

    void receive_subseg_pending()
    {
        int idx = cur_result.getIdx();
        for (int i = 0; i < client.cache.cacheinfo.seg_size; i++)
        {
            DateTime start = DateTime.Now;
            if (packet.misslist.Left != QUALITY.EMPTY)
            {
                int viewsize = readPosByteSize(messageByteLength);
                readFrameByteArray(viewsize);
                client.cache.table[idx].setlView(i, jpegBytes);
            }
            if (packet.misslist.Front != QUALITY.EMPTY)
            {
                int viewsize = readPosByteSize(messageByteLength);
                readFrameByteArray(viewsize);
                client.cache.table[idx].setfView(i, jpegBytes);
                UnityEngine.Debug.Log(viewsize + " is viewsize");
            }
            if (packet.misslist.Right != QUALITY.EMPTY)
            {
                int viewsize = readPosByteSize(messageByteLength);
                readFrameByteArray(viewsize);
                client.cache.table[idx].setrView(i, jpegBytes);
            }
            if (packet.misslist.Back != QUALITY.EMPTY)
            {
                int viewsize = readPosByteSize(messageByteLength);
                readFrameByteArray(viewsize);
                client.cache.table[idx].setbView(i, jpegBytes);
            }
            UnityEngine.Debug.LogFormat("{0} 번째 view 수신 완료 {1:f3}  misslist : {2}", i, (DateTime.Now - start).TotalMilliseconds, packet.misslist.createStringType());
        }
    }

    void Rendering()
    {
        int err = Client.decoding(jpegBytes, framebuffer, jpegBytes.Length, view_info.width, view_info.height, view_info.bpp);
        tex.LoadRawTextureData(framebuffer);
        tex.Apply();
    }
    void partialRendering()
    {
        partial_rendering = new Thread(() =>
        {
            int idx = cur_result.getIdx();
            int start = 0;
            int end = 0;
            bool reverse = false;
            int iter = cur_loc.iter;
            if(iter == 0)
            {
                start = 0; end = client.cache.cacheinfo.seg_size;
                reverse = false;
            }
            else if(iter == client.cache.cacheinfo.seg_size-1)
            {
                start = client.cache.cacheinfo.seg_size-1; end = 0;
                reverse = true;
                UnityEngine.Debug.LogError("HELLO");
                //start = 0; end = client.cache.cacheinfo.seg_size;
                //reverse = false;
            }
            if (!reverse)
            {
                for (int i = start; i < end; i++)
                {
                    lock (partialLocks[i])
                    {
                        client.rendersubviews(client.cache.table[idx].getlview(i),
                            client.cache.table[idx].getfview(i),
                            client.cache.table[idx].getrview(i),
                            client.cache.table[idx].getbview(i),
                            cur_result.getHitlist(), i);
                    }
                }
            }
            else
            {
                for (int i = start; i >= end; i--)
                {
                    lock (partialLocks[i])
                    {
                        client.rendersubviews(client.cache.table[idx].getlview(i),
                            client.cache.table[idx].getfview(i),
                            client.cache.table[idx].getrview(i),
                            client.cache.table[idx].getbview(i),
                            cur_result.getHitlist(), i);
                    }
                }
            }
            
            
        });
        partial_rendering.IsBackground = true;
        partial_rendering.Start();
    }
    void Renderings()
    {
        int iter = cur_loc.iter;
        int idx = cur_result.getIdx();
        renderingThread = new Thread(() => RenderingView(iter, idx));
        renderingThread.IsBackground = true;
        renderingThread.Start();
        renderingThread.Join();
        
        tex.LoadRawTextureData(client.render_frame());
        tex.Apply();
    }
    private byte[] readFrameByteArray(int size)
    {
        bool disconnected = false;

        jpegBytes = new byte[size];
        texClient.NoDelay = true;
        texClient.ReceiveBufferSize = size;
        NetworkStream tex_stream = texClient.GetStream();
        var total = 0;
        do
        {
            var read = tex_stream.Read(jpegBytes, total, size - total);
            if (read == 0)
            {
                disconnected = true;
                break;
            }
            total += read;
        } while (total != size);
        return jpegBytes;
    }

    private void readFrameLeft(int size)
    {
        bool disconnected = false;

        Lsubview = new byte[size];
        texClient.NoDelay = true;
        texClient.ReceiveBufferSize = size;
        NetworkStream tex_stream = texClient.GetStream();
        var total = 0;
        do
        {
            var read = tex_stream.Read(Lsubview, total, size - total);
            if (read == 0)
            {
                disconnected = true;
                break;
            }
            total += read;
        } while (total != size);
    }
    private void readFrameFront(int size)
    {
        bool disconnected = false;

        Fsubview = new byte[size];
        texClient.NoDelay = true;
        texClient.ReceiveBufferSize = size;
        NetworkStream tex_stream = texClient.GetStream();
        var total = 0;
        do
        {
            var read = tex_stream.Read(Fsubview, total, size - total);
            if (read == 0)
            {
                disconnected = true;
                break;
            }
            total += read;
        } while (total != size);
    }
    private void readFrameRight(int size)
    {
        bool disconnected = false;

        Rsubview = new byte[size];
        texClient.NoDelay = true;
        texClient.ReceiveBufferSize = size;
        NetworkStream tex_stream = texClient.GetStream();
        var total = 0;
        do
        {
            var read = tex_stream.Read(Rsubview, total, size - total);
            if (read == 0)
            {
                disconnected = true;
                break;
            }
            total += read;
        } while (total != size);
    }
    private void readFrameBack(int size)
    {
        bool disconnected = false;

        Bsubview = new byte[size];
        texClient.NoDelay = true;
        texClient.ReceiveBufferSize = size;
        NetworkStream tex_stream = texClient.GetStream();
        var total = 0;
        do
        {
            var read = tex_stream.Read(Bsubview, total, size - total);
            if (read == 0)
            {
                disconnected = true;
                break;
            }
            total += read;
        } while (total != size);
    }
    public List<SubRange> calcSubrange(int seg_size)
    {
        List<SubRange> subrange = new List<SubRange>();
        int numOfrange = 120 / seg_size;
        for (int iter = 0; iter < numOfrange; iter++)
        {
            int start = seg_size * (iter);
            int end = seg_size * (iter + 1) - 1;
            SubRange range = new SubRange(start, end);
            subrange.Add(range);
        }
        return subrange;
    }

    public string GetLocalIP()
    {
        string localIP = "Not available, please check your network seetings!";
        IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (IPAddress ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                localIP = ip.ToString();
                break;
            }
        }
        return localIP;
    }


    void EstablishConn()
    {
        #region Postional data connection
        posServer = new TcpListener(IPAddress.Any, Pos_port);
        posServer.Start();
        Task serverThread = new Task(() =>
        {
            UnityEngine.Debug.Log("Wait for sending pos data");
            posClient = posServer.AcceptTcpClient();
            UnityEngine.Debug.Log("Ready for sending pos data");
            pos_stream = posClient.GetStream();
        });
        serverThread.Start();
        UnityEngine.Debug.Log("My ip : " + GetLocalIP());
        #endregion

        Thread.Sleep(2000);

        #region view data connection
        texClient = new TcpClient();
        texClient.Connect(IPAddress.Parse(Texture_server_IP), Texture_port);
        tex_stream = texClient.GetStream();
        UnityEngine.Debug.Log("Ready for receiving texture data");
        #endregion
    }

    void byteLengthToFrameByteArray(int byteLength, byte[] fullBytes)
    {
        //Clear old data
        Array.Clear(fullBytes, 0, fullBytes.Length);
        //Convert int to bytes
        byte[] bytesToSendCount = BitConverter.GetBytes(byteLength);
        //Copy result to fullBytes
        bytesToSendCount.CopyTo(fullBytes, 0);
    }
    private int readPosByteSize(int size)
    {
        bool disconnected = false;

        byte[] PosBytesCount = new byte[size];
        texClient.NoDelay = true;
        texClient.ReceiveBufferSize = size;
        NetworkStream tex_stream = texClient.GetStream();
        var total = 0;
        do
        {
            var read = tex_stream.Read(PosBytesCount, total, size - total);
            if (read == 0)
            {
                disconnected = true;
                break;
            }
            total += read;
        } while (total != size);

        int byteLength;

        if (disconnected)
        {
            byteLength = -1;
        }
        else
        {
            byteLength = frameByteArrayToByteLength(PosBytesCount);
        }

        return byteLength;
    }
    
    void displayPos()
    {
        UnityEngine.Debug.LogFormat("Current position : {0},{1}", packet.pos.getX(), time);
    }
    int frameByteArrayToByteLength(byte[] frameBytesLength)
    {
        int byteLength = BitConverter.ToInt32(frameBytesLength, 0);
        return byteLength;
    }

    private void OnApplicationQuit()
    {
        for(int i = 0; i < receiveLocks.Length; i++)
        {
            receiveThreads[i].Join();
        }

        UnityEngine.Debug.LogWarningFormat("Avg end to end delay : {0:f3}", (e2eDelay / (double)client.pathlist.Length));
        //receiveThread.Join();
    }

    #region Convert struct to byte array
    public byte[] StructToBytes(object obj)
    {
        int iSize = Marshal.SizeOf(obj);

        byte[] arr = new byte[iSize];

        IntPtr ptr = Marshal.AllocHGlobal(iSize);
        Marshal.StructureToPtr(obj, ptr, false);
        Marshal.Copy(ptr, arr, 0, iSize);
        Marshal.FreeHGlobal(ptr);

        return arr;
    }

    public T ByteToStruct<T>(byte[] buffer) where T : struct
    {
        int size = Marshal.SizeOf(typeof(T));

        if (size > buffer.Length)
        {
            throw new Exception();
        }

        IntPtr ptr = Marshal.AllocHGlobal(size);
        Marshal.Copy(buffer, 0, ptr, size);
        T obj = (T)Marshal.PtrToStructure(ptr, typeof(T));
        Marshal.FreeHGlobal(ptr);
        return obj;
    }
    #endregion

}
