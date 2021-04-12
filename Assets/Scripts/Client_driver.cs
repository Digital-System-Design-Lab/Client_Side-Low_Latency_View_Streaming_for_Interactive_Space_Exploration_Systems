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
using System.Diagnostics;

public class Client_driver : MonoBehaviour
{
    #region Network variables
    [Header("Enter the server ip address and port for connection")]
    public string ContentServerIP = "165.246.39.163";
    public int ContentServerPort = 11000;

    [Header("Must be the same in sender and receiver")]
    public int messageByteLength = 24;
    byte[] frameBytesLength;

    TcpClient tcpclient = null;
    NetworkStream stream = null; //bi-directional stream
    byte[] posBytes;
    byte[] infoBytes;
    #endregion

    #region Positional Data
    ISESESAS.Path cur_path;
    Pos cur_pos;
    Loc cur_loc;
    Loc prev_loc;
    out_cache_search cur_result;
    Request packet;
    Client_info client_info;
    #endregion

    #region 기타 변수들
    int time = 0;
    public List<SubRange> range;
    Point2D[] predict_p;
    Velocity velo;

    int prev_seg_x;
    int prev_seg_y;

    bool skip = false;

    Stopwatch total_sw;
    Stopwatch bufferRead;

    int sumOfBufferRead;
    int sumOfViewRead;
    int[] target_delay;
    #endregion

    #region 수행시간 측정 변수들
    DateTime end2end;
    Profiler profiler;
    #endregion

    #region 클래스 변수들
    Client client;
    Cacheinfo cacheinfo;
    ESASinfo esasinfo;
    viewinfo view_info;
    ISESESAS.Path path;
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
    Thread[] receiveThreads;
    Thread renderingThread;

    object[] receiveLocks;
    object[] partialLocks;
    object receivelock;
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
        shareClientInfo();
    }

    // Update is called once per frame
    void Update()
    {
#if true
        List<Labelinfo> label_delay = new List<Labelinfo>();
        total_sw.Start();
        #region Request a view
        profiler.Start("getPos");
        getPos(time);                           // 정해진 scenario가 아닌 컨트롤러, 키보드를 인터페이스로 지정하려면 해당 함수 수정해야 한다.
        label_delay.Add(profiler.End());

        profiler.Start("Packetize");
        packetize();
        label_delay.Add(profiler.End());

        // 새로운 sub-segment에 위치하고 cache hit가 아닌 경우 서버로부터 sub-segment 데이터 요청
        if (!_isSameSeg() && cur_result.getStat() != CacheStatus.HIT)
        {
            profiler.Start("sendPacket");
            send_Packet();                          // Request Packet 전송
            label_delay.Add(profiler.End());

            skip = true;
            #region Receive a view
            // 만약 partial hit의 경우 miss난 sub-view set을 요청과 함께 background로 cache에 있는 sub-view decoding & up-sampling 작업 진행
            if (cur_result.getStat() == CacheStatus.PARTIAL_HIT)
            {
                profiler.Start("partialRendering");
                partialRendering();
                label_delay.Add(profiler.End()); 
            }
            // 서버로부터 miss난 sub-view set 요청
            profiler.Start("receive_subseg");
            receive_subseg();
            label_delay.Add(profiler.End());

            
            #endregion
        }
        #endregion

        #region Rendering(decode&up-sampling -> apply texture)
        //sub-segment의 첫 번째 view는 rendering 생략
        if (skip)
        {
            profiler.Start("Rendering_Skip");
            tex.Apply();
            skip = false;
            label_delay.Add(profiler.End());
        }
        else
        {
            profiler.Start("Rendering");
            Renderings();
            label_delay.Add(profiler.End());

        }
        #endregion
        if (time == client.pathlist.Length)     // Scenario가 모두 끝났다면 종료
        {
            EditorApplication.isPlaying = false;
        }
        time++;


        profiler.Start("recordPrevPacket");
        client.cache.recordPrevPacket(packet);
        client.cache.recordPrevPacket(cur_pos, cur_result, cur_loc);
        label_delay.Add(profiler.End());

        prev_loc = cur_loc;
        total_sw.Stop();

        // 일정한 End to end latency를 위해 target latency보다 빠르게 수행됐다면 idle 상태로 조절
        if (total_sw.ElapsedMilliseconds < target_delay[cur_pos.getEslevel()])
        {
            profiler.Start("idle");
            Thread.Sleep(target_delay[cur_pos.getEslevel()] - (int)total_sw.ElapsedMilliseconds);
            label_delay.Add(profiler.End());
        }
        total_sw.Reset();

        //profiler에 기록
        profiler.recording(label_delay, cur_pos, cur_result, cur_loc);
        profiler.recordLabel(label_delay);
#endif
    }

    #region Initalization methods
    /// <summary>
    /// 클라이언트 정보를 서버에게 전달을 하여 sub-segment 크기를 공유
    /// </summary>
    public void shareClientInfo()
    {
        frameBytesLength = new byte[messageByteLength];
        infoBytes = StructToBytes(client_info);

        byteLengthToFrameByteArray(infoBytes.Length, frameBytesLength);
        tcpclient.NoDelay = true;
        tcpclient.Client.NoDelay = true;
        tcpclient.SendBufferSize = frameBytesLength.Length;
        NetworkStream pos_stream = tcpclient.GetStream();
        pos_stream.Write(frameBytesLength, 0, frameBytesLength.Length);
        pos_stream.Write(infoBytes, 0, infoBytes.Length);
    }
    /// <summary>
    /// Thread 선언 및 lock 객체 선언 
    /// </summary>
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
    }

    /// <summary>
    /// Class 와 Struct 변수 초기화 함수
    /// </summary>
    void init_ClassandStruct()
    {
        int one_way_length = 5520;
        double percentage = 0.8f;
        int cachesize = (int)((double)(one_way_length) * percentage);
        client = new Client();                                                          //클라이언트 객체 생성 
        profiler = new Profiler();                                                      //수행시간 분석을 위한 Profiler 객체 생성
        view_info = new viewinfo(2048, 4096, 3);
        int seg_size = 12;
        cacheinfo = new Cacheinfo(cachesize, seg_size, 720, 6720, Policy.GDC, 10);
        esasinfo = new ESASinfo(6, 0.3f, 0.6f);
        client.init(cacheinfo, esasinfo, view_info);
        client.read_path("Worst_cycle.txt");                                            //벤치마킹을 위한 walking scenario
        framebuffer = new byte[view_info.width * view_info.height * view_info.bpp];
        range = calcSubrange(seg_size);
        cur_result = new out_cache_search();
        client_info = new Client_info(seg_size, client.pathlist.Length);
        prev_seg_x = -1;
        prev_seg_y = -1;
        total_sw = new Stopwatch();
        bufferRead = new Stopwatch();
        target_delay = new int[3];
        target_delay[0] = 33; // Slow 33ms
        target_delay[1] = 20; // Fast 20ms
        target_delay[2] = 15; // SuperFast 15ms
    }
    void init_Unity()
    {
        Render_tex = GameObject.Find("LFSpace");
        Equi = GameObject.Find("Equi");
        Render_tex.GetComponent<Renderer>().enabled = true;
        Equi.GetComponent<Renderer>().enabled = true;
        Render_tex.GetComponent<Renderer>().material.shader = Shader.Find("Unlit/Pano360Shader");
        Equi.GetComponent<Renderer>().material.shader = Shader.Find("Unlit/Pano360Shader");
        tex = new Texture2D(view_info.width, view_info.height, TextureFormat.RGB24, false);         //생성할 view의 해상도로 Texture를 설정. 만약, 그렇게하지 않으면 apply 속도가 추가적으로 발생
        Render_tex.GetComponent<Renderer>().material.mainTexture = tex;
        Equi.GetComponent<Renderer>().material.mainTexture = tex;
    }

    #endregion

    #region Rendering methods
    void RenderingView(int iter, int index)
    {
        int temp = iter;
        int idx = index;
        //Partial rendering과 receive thread가 수행되지 않았다면 기다렸다가 rendering하기 위해 lock을 이용
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
            lock (receiveLocks[temp])
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

            // 정방향인지 역방향인지 판별
            if (iter == 0)
            {
                start = 0; end = client.cache.cacheinfo.seg_size;
                reverse = false;
            }
            else if (iter == client.cache.cacheinfo.seg_size - 1)
            {
                start = client.cache.cacheinfo.seg_size - 1; end = 0;
                reverse = true;
            }

            // Cache에 있는 sub-view에 대해 background에서 rendering 수행
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
        renderingThread = new Thread(() => RenderingView(iter, idx));       // sub-view를 하나의 frame image로 rendering
        renderingThread.IsBackground = true;
        renderingThread.Start();
        renderingThread.Join();

        // Texture2D 데이터에 frame을 입히는 작업
        tex.LoadRawTextureData(client.render_frame());
        tex.Apply();
    }
    #endregion

    #region 기타 methods
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
        int hd = client.getHeadDirection(cur_path.hd);                              //0~360도 head direction을 0~3의 head direction으로 변환
        int esl = client.calcESL(cur_path);                                         //Exploring Speed Level (ESL)인 Slow, Fast, Super-fast를 각각 0, 1, 2로 변환
        string reqlist = client.getReqViewlist(esl, hd);                            //esl과 hd에 따른 필요한 sub-view list를 string으로 반환 예) 0222 -> (LEFT=0)(FRONT=2)(RIGHT=2)(BACK=2) 0은 전송하지 않고 1은 Down-sampled, 2는 Original sub-view를 의미
        cur_pos = new Pos(cur_path.x, cur_path.y, hd, esl);
        cur_loc = TransP2L();                                                       //가상공간의 위치인 Loc으로  변환

        client.CacheTableUpdate(packet, cur_loc, reqlist, time, ref cur_result);    //Cache Table update
        misslist.convertSTR2LIST(cur_result.getMisslist());                         //network 전송을 위해 List로 변환
        packet = new Request(cur_pos, misslist, cur_loc.get_seg_pos());             //Request 패킷 변환

        // Dead Reckoning 인 경우 prediction 진행
        int isPredict = 0;
        if (client.cache.cacheinfo.policy == Policy.DR)
        {

            if (time == 0)
            {
                velo = new Velocity(0.0f, 0.0f);
            }
            else
            {
                velo = client.cache.calcVelocity(packet, prev_loc);
            }
            if (!prediction())
            {
                isPredict = 1;
            }
        }
        // Prediction 결과 packet에 초기화
        packet.isPredicted = isPredict;

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
    public bool prediction()
    {
        bool isPredict = true;
        bool isPartial = false;
        if (time != 0 && time != (client.pathlist.Length - 1))
        {
            if (cur_pos.getHead_dir() == 1 || cur_pos.getHead_dir() == 2)
            {
                if (cur_loc.iter == cacheinfo.seg_size - 1)
                {
                    //prediction을 한다.
                    predict_p = client.cache.predictPos(packet, velo);
                    for (int i = 0; i < 10; i++)
                    {
                        UnityEngine.Debug.LogWarningFormat("{0} {1} predicted position", predict_p[i].getX(), predict_p[i].getY());
                    }

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

        return isPredict;
    }
    #endregion

    #region Network methods
    void ReceiveView(Request _packet, int idx, int i)
    {
        // sub-segment size 만큼 recevie한다. 단, cache 저장될 때 순서에 유의해야하기 때문에 thread lock을 이용하여 통제
        lock (receiveLocks[i])
        {
            lock (receivelock)
            {
                int total = 0;
                int num = readPosByteSize(messageByteLength);
                //int num = i;
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
            }
        }
    }
    void receive_subseg()
    {
        int index = cur_result.getIdx();
        Request _packet = packet;

        bool reverse = false;
        if (cur_loc.iter == 0)
        {
            reverse = false;
        }
        else if (cur_loc.iter == client.cache.cacheinfo.seg_size - 1)
        {
            reverse = true;
        }

        // 서버로부터 요청한 sub-segment 데이터 수신
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
            for (int i = receiveThreads.Length - 1; i >= 0; i--)
            {
                int iter = i;
                receiveThreads[iter] = new Thread(() => ReceiveView(_packet, index, iter));
                receiveThreads[iter].IsBackground = true;
                receiveThreads[iter].Start();
            }
        }
    }
    void send_Packet()
    {
        frameBytesLength = new byte[messageByteLength];
        posBytes = packet.StructToBytes(packet);

        byteLengthToFrameByteArray(posBytes.Length, frameBytesLength);
        tcpclient.NoDelay = true;
        tcpclient.Client.NoDelay = true;
        tcpclient.SendBufferSize = frameBytesLength.Length;
        NetworkStream pos_stream = tcpclient.GetStream();
        pos_stream.Write(frameBytesLength, 0, frameBytesLength.Length);
        pos_stream.Write(posBytes, 0, posBytes.Length);
    }
    private void readFrameLeft(int size)
    {
        bool disconnected = false;

        Lsubview = new byte[size];
        tcpclient.NoDelay = true;
        tcpclient.Client.NoDelay = true;
        tcpclient.ReceiveBufferSize = size;
        NetworkStream tex_stream = tcpclient.GetStream();
        var total = 0;
        do
        {

        } while (!tex_stream.DataAvailable);
        bufferRead.Start();
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
        bufferRead.Stop();
        sumOfBufferRead += (int)bufferRead.ElapsedMilliseconds;
        bufferRead.Reset();
    }
    private void readFrameFront(int size)
    {
        bool disconnected = false;

        Fsubview = new byte[size];
        tcpclient.NoDelay = true;
        tcpclient.Client.NoDelay = true;
        tcpclient.ReceiveBufferSize = size;
        NetworkStream tex_stream = tcpclient.GetStream();
        var total = 0;
        do
        {

        } while (!tex_stream.DataAvailable);
        bufferRead.Start();
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
        //UnityEngine.Debug.LogWarningFormat("Front read time : {0}", exetime.ElapsedMilliseconds);
        bufferRead.Stop();
        sumOfBufferRead += (int)bufferRead.ElapsedMilliseconds;
        bufferRead.Reset();
    }
    private void readFrameRight(int size)
    {
        bool disconnected = false;

        Rsubview = new byte[size];
        tcpclient.NoDelay = true;
        tcpclient.Client.NoDelay = true;
        tcpclient.ReceiveBufferSize = size;
        NetworkStream tex_stream = tcpclient.GetStream();
        var total = 0;
        do
        {

        } while (!tex_stream.DataAvailable);
        bufferRead.Start();
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
        //UnityEngine.Debug.LogWarningFormat("Right read time : {0}", exetime.ElapsedMilliseconds);
        bufferRead.Stop();
        sumOfBufferRead += (int)bufferRead.ElapsedMilliseconds;
        bufferRead.Reset();
    }
    private void readFrameBack(int size)
    {
        bool disconnected = false;

        Bsubview = new byte[size];
        tcpclient.NoDelay = true;
        tcpclient.Client.NoDelay = true;
        tcpclient.ReceiveBufferSize = size;
        NetworkStream tex_stream = tcpclient.GetStream();
        var total = 0;
        do
        {

        } while (!tex_stream.DataAvailable);
        bufferRead.Start();
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
        //UnityEngine.Debug.LogWarningFormat("Back read time : {0}", exetime.ElapsedMilliseconds);
        bufferRead.Stop();
        sumOfBufferRead += (int)bufferRead.ElapsedMilliseconds;
        bufferRead.Reset();
    }
    /// <summary>
    /// Server와의 연결
    /// </summary>
    void EstablishConn()
    {
        tcpclient = new TcpClient();
        tcpclient.Connect(IPAddress.Parse(ContentServerIP), ContentServerPort);
        stream = tcpclient.GetStream();
        UnityEngine.Debug.Log("Connection is successful !!\n");
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
        tcpclient.NoDelay = true;
        tcpclient.Client.NoDelay = true;
        tcpclient.ReceiveBufferSize = size;
        NetworkStream tex_stream = tcpclient.GetStream();
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

    int frameByteArrayToByteLength(byte[] frameBytesLength)
    {
        int byteLength = BitConverter.ToInt32(frameBytesLength, 0);
        return byteLength;
    }
    #endregion

    private void OnApplicationQuit()
    {
        for (int i = 0; i < receiveLocks.Length; i++)
        {
            receiveThreads[i].Join();
        }
        profiler.getSummary();
        profiler.writeSummary();
        profiler.writeDetail();
        profiler.writeLabel();
        //UnityEngine.Debug.LogWarningFormat("Avg Buffer Read time : {0:f3}", (double)sumOfBufferRead/ (double)client.pathlist.Length);
        //UnityEngine.Debug.LogWarningFormat("Avg View Read time : {0:f3}", (double)sumOfViewRead/ (double)client.pathlist.Length);
        //UnityEngine.Debug.LogWarningFormat("Avg end to end delay : {0:f3}", (e2eDelay / (double)client.pathlist.Length));
        //receiveThread.Join();
        tcpclient.Close();
        GC.Collect();
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
