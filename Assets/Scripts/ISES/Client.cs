using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.InteropServices;
using ISESCache;
using ISESESAS;
using ISESStructure;
using UnityEngine;
using _NS_5DoFVR;


namespace ISESClient
{
    class Client
    {
        #region DllImport
        [DllImport("libjpeg_turbo_Dll")]
        public  static extern int decoding(byte[] input, byte[] output, int size, int width, int height, int bpp);
        [DllImport("libjpeg_turbo_Dll")]
        public static extern void Resize(byte[] src, byte[] dst, int size, int owidth, int oheight, int rwidth, int rheight);
        [DllImport("libjpeg_turbo_Dll")]
        public static extern void Resize_lite(byte[] src, byte[] dst, int size, int owidth, int oheight, int rwidth, int rheight);
        [DllImport("libjpeg_turbo_Dll")]
        public static extern void Resize_Slite(byte[] src, byte[] dst, int size, int owidth, int oheight, int rwidth, int rheight);
        [DllImport("libjpeg_turbo_Dll")]
        private static extern void Resizing_Parall(byte[] input, byte[] output, int size, int owidth, int oheight, int rwidth, int rheight, int threadnum, int order, int bpp);


        #endregion

        int interface_mode = 0; //0 : test mode, 1 : user mode

        #region ProposedScheme Class
        public GDC cache;
        public ESAS esas;
        #endregion

        #region UnityObjects
        
        #endregion

        #region addtionalclass vars
        public XboxController xbox;
        public NS_5DoFVR myVR;
        public view_container viewContainer;
        public List<view_container> viewContainers;
        #endregion

        #region essential vars
        public viewinfo info;
        public Path[] pathlist;
        public List<SubRange> range;
        public black_imgs black_img;
        #endregion

        #region Byte variables
        #region JPEG buffers
        byte[] temp_viewL;
        byte[] temp_viewF;
        byte[] temp_viewR;
        byte[] temp_viewB;
        #endregion

        #region Sub-view buffer
        byte[] decoded_viewL;
        byte[] decoded_viewF;
        byte[] decoded_viewR;
        byte[] decoded_viewB;


        List<byte[]> Ptemp_viewL;
        List<byte[]> Ptemp_viewF;
        List<byte[]> Ptemp_viewR;
        List<byte[]> Ptemp_viewB;
        List<byte[]> PDSsubviewL;
        List<byte[]> PDSsubviewF;
        List<byte[]> PDSsubviewR;
        List<byte[]> PDSsubviewB;
        List<byte[]> Pdecoded_viewL;
        List<byte[]> Pdecoded_viewF;
        List<byte[]> Pdecoded_viewR;
        List<byte[]> Pdecoded_viewB;
        #endregion

        #region Sub-view_DS buffer
        byte[] DSsubviewL;
        byte[] DSsubviewF;
        byte[] DSsubviewR;
        byte[] DSsubviewB;
        #endregion

        byte[] temp_viewOnlyDec;
        byte[] DSsubviewOnlyDec;
        byte[] decoded_viewOnlyDec;

        byte[] temp;
        byte[] pre_frame;

        #region Frame buffer
        byte[] framebuffer;
        #endregion

        #endregion

        #region Tasklist
        public Task[] resizelistL;
        public Task[] resizelistF;
        public Task[] resizelistR;
        public Task[] resizelistB;
        public Thread[] Threadlist;
        public Thread[] PThreadlist;
        #endregion

        public string init(Cacheinfo cacheinfo, ESASinfo esasinfo, viewinfo info)
        {
            this.info = info;
            
            #region initialize objects related schemes
            cache = new GDC();
            esas = new ESAS(esasinfo);
            cache.init(cacheinfo);
            range = calcSubrange(cacheinfo.seg_size);
            black_img.init(info.width / 4, info.height, info.bpp);
            viewContainer = new view_container(4096, 2048, 4, 2, 3);
            viewContainers = new List<view_container>();

            for(int i = 0; i < cacheinfo.seg_size; i++)
            {
                view_container temp = new view_container(4096, 2048, 4, 2, 3);
                viewContainers.Add(temp);
            }
            #endregion

            #region initialize additional classes
            myVR = new NS_5DoFVR();
            myVR.parsing_data();
            xbox = new XboxController();
            #endregion

            #region initialize byte arrays
            DSsubviewL = new byte[((info.width / 4) / 2) * ((info.height) / 2) * info.bpp];
            DSsubviewF = new byte[((info.width / 4) / 2) * ((info.height) / 2) * info.bpp];
            DSsubviewR = new byte[((info.width / 4) / 2) * ((info.height) / 2) * info.bpp];
            DSsubviewB = new byte[((info.width / 4) / 2) * ((info.height) / 2) * info.bpp];
            DSsubviewOnlyDec = new byte[((info.width / 4) / 2) * ((info.height) / 2) * info.bpp];
            decoded_viewL = new byte[(info.width / 4) * (info.height) * info.bpp];
            decoded_viewF = new byte[(info.width / 4) * (info.height) * info.bpp];
            decoded_viewR = new byte[(info.width / 4) * (info.height) * info.bpp];
            decoded_viewB = new byte[(info.width / 4) * (info.height) * info.bpp];
            decoded_viewOnlyDec = new byte[(info.width / 4) * (info.height) * info.bpp];
            framebuffer = new byte[info.width * info.height * info.bpp];
            pre_frame = new byte[info.width * info.height * info.bpp];
            #endregion
            Ptemp_viewL = new List<byte[]>();
            Ptemp_viewF = new List<byte[]>();
            Ptemp_viewR = new List<byte[]>();
            Ptemp_viewB = new List<byte[]>();
            PDSsubviewL = new List<byte[]>();
            PDSsubviewF = new List<byte[]>();
            PDSsubviewR = new List<byte[]>();
            PDSsubviewB = new List<byte[]>();
            Pdecoded_viewL = new List<byte[]>();
            Pdecoded_viewF = new List<byte[]>();
            Pdecoded_viewR = new List<byte[]>();
            Pdecoded_viewB = new List<byte[]>();

            for (int i = 0; i < cache.cacheinfo.seg_size; i++)
            {
                byte[] ltemp = new byte[(info.width / 4) * (info.height) * info.bpp];
                byte[] ftemp = new byte[(info.width / 4) * (info.height) * info.bpp];
                byte[] rtemp = new byte[(info.width / 4) * (info.height) * info.bpp];
                byte[] btemp = new byte[(info.width / 4) * (info.height) * info.bpp];

                byte[] DStempL = new byte[((info.width / 4) / 2) * ((info.height) / 2) * info.bpp];
                byte[] DStempF = new byte[((info.width / 4) / 2) * ((info.height) / 2) * info.bpp];
                byte[] DStempR = new byte[((info.width / 4) / 2) * ((info.height) / 2) * info.bpp];
                byte[] DStempB = new byte[((info.width / 4) / 2) * ((info.height) / 2) * info.bpp];

                byte[] tempL = null;
                byte[] tempF = null;
                byte[] tempR = null;
                byte[] tempB = null;

                Ptemp_viewL.Add(tempL);
                Ptemp_viewF.Add(tempF);
                Ptemp_viewR.Add(tempR);
                Ptemp_viewB.Add(tempB);

                PDSsubviewL.Add(DStempL);
                PDSsubviewF.Add(DStempF);
                PDSsubviewR.Add(DStempR);
                PDSsubviewB.Add(DStempB);

                Pdecoded_viewL.Add(ltemp);
                Pdecoded_viewF.Add(ftemp);
                Pdecoded_viewR.Add(rtemp);
                Pdecoded_viewB.Add(btemp);
            }
            #region initialize Task lists
            resizelistL = new Task[8];
            resizelistF=new Task[8];
            resizelistR=new Task[8];
            resizelistB=new Task[8];

            Threadlist = new Thread[4];
            PThreadlist = new Thread[4];
            #endregion

            return "[Client] initialization process was completed...";
        }
        #region Positional processes
        public int getHeadDirection(int dir)
        {
            int hd = 0;
            if ((dir >= 0 && dir < 30) || (dir >= 330 && dir <= 360))
            {
                hd = 2; //RIGHT
            }
            else if ((dir >= 60 && dir < 120))
            {
                hd = 1; //FRONT
            }
            else if ((dir >= 150 && dir < 210))
            {
                hd = 0; //LEFT
            }
            else if ((dir >= 240 && dir < 300))
            {
                hd = 3; //BACK
            }
            return hd;
        }
        public int calcESL(Path currentPos, int time)
        {
            esas.addTuple(currentPos);
            float es = esas.calc_es();
            int esl = esas.calcesl(es);
            return esl;
        }
        public int calcESL(Path currentPos)
        {
            float es = currentPos.acc;      //사용자의 가속도
            int esl = esas.calcesl(es);     //사용자의 가속도를 기반으로 Exploring Speed Level 계산
            return esl;
        }

        public string getReqViewlist(int esl, int hd)
        {
            string resultlist = esas.calcviewlist(esl, hd);
            return resultlist;
        }
        #endregion

        #region 10.27 작업 관련
        public void CacheTableUpdate(Request packet, Loc cur_loc, string reqlist, int time, ref out_cache_search result)
        {
            //Search
            int idx = cache.find_records(cur_loc); // cache에 있는 record 찾기
            //status 확인하기
            result.setIdx(idx);
            cache.checkStat(reqlist, ref result);

            //table 정보 update
            cache.updateTable(packet, cur_loc, ref result, time);
        }
        #endregion


        #region other methods
        public void read_path(string filename)
        {
            string[] textValue = System.IO.File.ReadAllLines(filename);
            string[] myString;
            if (textValue.Length > 0)
            {
                pathlist = new Path[textValue.Length];
                for(int iter = 0; iter < textValue.Length;iter++)
                {
                    myString = textValue[iter].Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    pathlist[iter].x = Convert.ToInt32(myString[0]);
                    pathlist[iter].y = Convert.ToInt32(myString[1]);
                    pathlist[iter].hd = Convert.ToInt32(myString[2]);
                    pathlist[iter].acc = (float)Convert.ToDouble(myString[3]);
                }
            }

        }
        public Path getPath(int time)
        {
            if(pathlist.Length <= time)
            {
                return pathlist[pathlist.Length - 1];
            }
            else
            {
                return pathlist[time];
            }
        }
        public List<SubRange> calcSubrange(int seg_size)
        {
            List<SubRange> subrange = new List<SubRange>();
            int numOfrange = 120 / seg_size;
            for(int iter = 0; iter < numOfrange; iter++)
            {
                int start = seg_size * (iter);
                int end = seg_size * (iter+1) - 1;
                SubRange range = new SubRange(start, end);
                subrange.Add(range);
            }
            return subrange;
        }
        public byte[] render_frame()
        {
            int one_eighth_length = viewContainer.owidth / 2 * viewContainer.bpp;
            int quater_length = one_eighth_length * 2;
            int back_offset = one_eighth_length;
            int front_offset = 0;
            int left_offset = 0;
            int right_offset = 0;
            int offset = 0;

            for (int h = 0; h < viewContainer.height; h++)
            {
                System.Buffer.BlockCopy(viewContainer.bsubview, back_offset, framebuffer, offset, one_eighth_length);
                back_offset -= one_eighth_length;
                offset += one_eighth_length;
                System.Buffer.BlockCopy(viewContainer.lsubview, left_offset, framebuffer, offset, quater_length);
                left_offset += quater_length;
                offset += quater_length;
                System.Buffer.BlockCopy(viewContainer.fsubview, front_offset, framebuffer, offset, quater_length);
                front_offset += quater_length;
                offset += quater_length;
                System.Buffer.BlockCopy(viewContainer.rsubview, right_offset, framebuffer, offset, quater_length);
                right_offset += quater_length;
                offset += quater_length;
                System.Buffer.BlockCopy(viewContainer.bsubview, back_offset, framebuffer, offset, one_eighth_length);
                back_offset += (quater_length + one_eighth_length);
                offset += one_eighth_length;
            }
            return framebuffer;
        }

        public void renderPartialsubviews(byte[] Lview, byte[] Fview, byte[] Rview, byte[] Bview, string misslist, string hitlist, int iter)
        {
            int[] missdigits = new int[4];
            int[] hitdigits = new int[4];
            bool[] is_created = new bool[4];
            bool[] isRendered = new bool[4];
            for (int i = 0; i < 4; i++)
            {
                is_created[i] = false;
            }

            DateTime[] stopwatch = new DateTime[4];
            double[] renderingtime = new double[4];

            for (int i = 0; i < 4; i++)
            {
                missdigits[i] = Convert.ToInt32(misslist.Substring(i, 1));
                hitdigits[i] = Convert.ToInt32(hitlist.Substring(i, 1));
                if(hitdigits[i] >= 1)
                {
                    //UnityEngine.Debug.LogErrorFormat("{0} 번째 partial rendering한거 연결해주는 중...", iter);
                    viewContainer.setView(i, viewContainers[iter].getView(i));
                }
                else if(hitdigits[i]==0)
                {
                    if (missdigits[i] == 1)
                    {
                        switch (i)
                        {
                            case 0:
                                Threadlist[0] = new Thread(() => renderpart_lDS(Lview));
                                is_created[0] = true;
                                Threadlist[0].IsBackground = true;
                                Threadlist[0].Start();
                                stopwatch[0] = DateTime.Now;
                                //renderpart_lDS(iter, subcontainer, container);
                                break;
                            case 1:
                                Threadlist[1] = new Thread(() => renderpart_fDS(Fview));
                                is_created[1] = true;
                                Threadlist[1].IsBackground = true;
                                Threadlist[1].Start();
                                stopwatch[1] = DateTime.Now;
                                //renderpart_fDS(iter, subcontainer, container);
                                break;
                            case 2:
                                Threadlist[2] = new Thread(() => renderpart_rDS(Rview));
                                is_created[2] = true;
                                Threadlist[2].IsBackground = true;
                                Threadlist[2].Start();
                                stopwatch[2] = DateTime.Now;
                                //renderpart_rDS(iter, subcontainer, container);
                                break;
                            case 3:
                                Threadlist[3] = new Thread(() => renderpart_bDS(Bview));
                                is_created[3] = true;
                                Threadlist[3].IsBackground = true;
                                Threadlist[3].Start();
                                stopwatch[3] = DateTime.Now;
                                //renderpart_bDS(iter, subcontainer, container);
                                break;
                        }
                    }
                    else if (missdigits[i] == 2)
                    {
                        switch (i)
                        {
                            case 0:
                                Threadlist[0] = new Thread(() => renderpart_l(Lview));
                                is_created[0] = true;
                                Threadlist[0].IsBackground = true;
                                Threadlist[0].Start();
                                //renderpart_l(iter, subcontainer, container);
                                stopwatch[0] = DateTime.Now;
                                break;
                            case 1:
                                Threadlist[1] = new Thread(() => renderpart_f(Fview));
                                is_created[1] = true;
                                Threadlist[1].IsBackground = true;
                                Threadlist[1].Start();
                                //renderpart_f(iter, subcontainer, container);
                                stopwatch[1] = DateTime.Now;
                                break;
                            case 2:
                                Threadlist[2] = new Thread(() => renderpart_r(Rview));
                                is_created[2] = true;
                                Threadlist[2].IsBackground = true;
                                Threadlist[2].Start();
                                //renderpart_r(iter, subcontainer, container);
                                stopwatch[2] = DateTime.Now;
                                break;
                            case 3:
                                Threadlist[3] = new Thread(() => renderpart_b(Bview));
                                is_created[3] = true;
                                Threadlist[3].IsBackground = true;
                                Threadlist[3].Start();
                                //renderpart_b(iter, subcontainer, container);
                                stopwatch[3] = DateTime.Now;
                                break;
                        }
                    }
                    else if (missdigits[i] == 0)
                    {
                        viewContainer.setView(i, black_img.getView(i));
                    }
                }
            }
            for (int i = 0; i < 4; i++)
            {
                if (is_created[i])
                {
                    Threadlist[i].Join();
                    renderingtime[i] = (DateTime.Now - stopwatch[i]).TotalMilliseconds;
                }
            }
        }
        public void rendersubviews(byte[] Lview, byte[] Fview, byte[] Rview, byte[] Bview, string list)
        {
            int[] digits = new int[4];
            bool[] is_created = new bool[4];
            bool[] isRendered = new bool[4];
            for (int i = 0; i < 4; i++)
            {
                is_created[i] = false;
            }

            DateTime[] stopwatch = new DateTime[4];
            double[] renderingtime = new double[4];

            for (int i = 0; i < 4; i++)
            {
                digits[i] = Convert.ToInt32(list.Substring(i, 1));
                if (digits[i] == 1)
                {
                    switch (i)
                    {
                        case 0:
                            Threadlist[0] = new Thread(() => renderpart_lDS(Lview));
                            is_created[0] = true;
                            Threadlist[0].IsBackground = true;
                            Threadlist[0].Start();
                            stopwatch[0] = DateTime.Now;
                            //renderpart_lDS(iter, subcontainer, container);
                            break;
                        case 1:
                            Threadlist[1] = new Thread(() => renderpart_fDS(Fview));
                            is_created[1] = true;
                            Threadlist[1].IsBackground = true;
                            Threadlist[1].Start();
                            stopwatch[1] = DateTime.Now;
                            //renderpart_fDS(iter, subcontainer, container);
                            break;
                        case 2:
                            Threadlist[2] = new Thread(() => renderpart_rDS(Rview));
                            is_created[2] = true;
                            Threadlist[2].IsBackground = true;
                            Threadlist[2].Start();
                            stopwatch[2] = DateTime.Now;
                            //renderpart_rDS(iter, subcontainer, container);
                            break;
                        case 3:
                            Threadlist[3] = new Thread(() => renderpart_bDS(Bview));
                            is_created[3] = true;
                            Threadlist[3].IsBackground = true;
                            Threadlist[3].Start();
                            stopwatch[3] = DateTime.Now;
                            //renderpart_bDS(iter, subcontainer, container);
                            break;
                    }
                }
                else if (digits[i] == 2)
                {
                    switch (i)
                    {
                        case 0:
                            Threadlist[0] = new Thread(() => renderpart_l(Lview));
                            is_created[0] = true;
                            Threadlist[0].IsBackground = true;
                            Threadlist[0].Start();
                            //renderpart_l(iter, subcontainer, container);
                            stopwatch[0] = DateTime.Now;
                            break;
                        case 1:
                            Threadlist[1] = new Thread(() => renderpart_f(Fview));
                            is_created[1] = true;
                            Threadlist[1].IsBackground = true;
                            Threadlist[1].Start();
                            //renderpart_f(iter, subcontainer, container);
                            stopwatch[1] = DateTime.Now;
                            break;
                        case 2:
                            Threadlist[2] = new Thread(() => renderpart_r(Rview));
                            is_created[2] = true;
                            Threadlist[2].IsBackground = true;
                            Threadlist[2].Start();
                            //renderpart_r(iter, subcontainer, container);
                            stopwatch[2] = DateTime.Now;
                            break;
                        case 3:
                            Threadlist[3] = new Thread(() => renderpart_b(Bview));
                            is_created[3] = true;
                            Threadlist[3].IsBackground = true;
                            Threadlist[3].Start();
                            //renderpart_b(iter, subcontainer, container);
                            stopwatch[3] = DateTime.Now;
                            break;
                    }
                }
                else if (digits[i] == 0)
                {
                    viewContainer.setView(i, black_img.getView(i));
                }

            }
            for (int i = 0; i < 4; i++)
            {
                if (is_created[i])
                {
                    Threadlist[i].Join();
                    renderingtime[i] = (DateTime.Now - stopwatch[i]).TotalMilliseconds;
                }
            }
        }
        public void rendersubviews(byte[] Lview, byte[] Fview, byte[] Rview, byte[] Bview, string list, int iter)
        {
            int[] digits = new int[4];
            bool[] is_created = new bool[4];
            bool[] isRendered = new bool[4];
            for (int i = 0; i < 4; i++)
            {
                is_created[i] = false;
            }

            DateTime[] stopwatch = new DateTime[4];
            double[] renderingtime = new double[4];
            
            for (int i = 0; i < 4; i++)
            {
                digits[i] = Convert.ToInt32(list.Substring(i, 1));
                if (digits[i] == 1)
                {
                    switch (i)
                    {
                        case 0:
                            PThreadlist[0] = new Thread(() => renderpart_lDS(Lview, iter));
                            is_created[0] = true;
                            PThreadlist[0].IsBackground = true;
                            PThreadlist[0].Start();
                            stopwatch[0] = DateTime.Now;
                            //renderpart_lDS(iter, subcontainer, container);
                            break;
                        case 1:
                            PThreadlist[1] = new Thread(() => renderpart_fDS(Fview, iter));
                            is_created[1] = true;
                            PThreadlist[1].IsBackground = true;
                            PThreadlist[1].Start();
                            stopwatch[1] = DateTime.Now;
                            //renderpart_fDS(iter, subcontainer, container);
                            break;
                        case 2:
                            PThreadlist[2] = new Thread(() => renderpart_rDS(Rview, iter));
                            is_created[2] = true;
                            PThreadlist[2].IsBackground = true;
                            PThreadlist[2].Start();
                            stopwatch[2] = DateTime.Now;
                            //renderpart_rDS(iter, subcontainer, container);
                            break;
                        case 3:
                            PThreadlist[3] = new Thread(() => renderpart_bDS(Bview, iter));
                            is_created[3] = true;
                            PThreadlist[3].IsBackground = true;
                            PThreadlist[3].Start();
                            stopwatch[3] = DateTime.Now;
                            //renderpart_bDS(iter, subcontainer, container);
                            break;
                    }
                }
                else if (digits[i] == 2)
                {
                    switch (i)
                    {
                        case 0:
                            PThreadlist[0] = new Thread(() => renderpart_l(Lview, iter));
                            is_created[0] = true;
                            PThreadlist[0].IsBackground = true;
                            PThreadlist[0].Start();
                            //renderpart_l(iter, subcontainer, container);
                            stopwatch[0] = DateTime.Now;
                            break;
                        case 1:
                            PThreadlist[1] = new Thread(() => renderpart_f(Fview, iter));
                            is_created[1] = true;
                            PThreadlist[1].IsBackground = true;
                            PThreadlist[1].Start();
                            //renderpart_f(iter, subcontainer, container);
                            stopwatch[1] = DateTime.Now;
                            break;
                        case 2:
                            PThreadlist[2] = new Thread(() => renderpart_r(Rview, iter));
                            is_created[2] = true;
                            PThreadlist[2].IsBackground = true;
                            PThreadlist[2].Start();
                            //renderpart_r(iter, subcontainer, container);
                            stopwatch[2] = DateTime.Now;
                            break;
                        case 3:
                            PThreadlist[3] = new Thread(() => renderpart_b(Bview, iter));
                            is_created[3] = true;
                            PThreadlist[3].IsBackground = true;
                            PThreadlist[3].Start();
                            //renderpart_b(iter, subcontainer, container);
                            stopwatch[3] = DateTime.Now;
                            break;
                    }
                }
            }
            for (int i = 0; i < 4; i++)
            {
                if (is_created[i])
                {
                    PThreadlist[i].Join();
                    renderingtime[i] = (DateTime.Now - stopwatch[i]).TotalMilliseconds;
                }
            }
        }

        public void pre_rendersubviews(pre_renderViews preRenderViews, string list, int iter, int idx)
        {
            int[] digits = new int[4];
            bool[] is_created = new bool[4];
            UnityEngine.Debug.LogErrorFormat("Hit list : {0} hit index : {1} iter : {2}", list, idx, iter);
            for (int i = 0; i < 4; i++)
            {
                is_created[i] = false;
            }
            for (int i = 0; i < 4; i++)
            {
                digits[i] = Convert.ToInt32(list.Substring(i, 1));
                if (digits[i] == 1)
                {
                    switch (i)
                    {
                        case 0:
                            Threadlist[0] = new Thread(() => renderpart_lDS(iter, preRenderViews, idx, i));
                            is_created[0] = true;
                            Threadlist[0].IsBackground = true;
                            Threadlist[0].Start();
                            break;
                        case 1:
                            Threadlist[1] = new Thread(() => renderpart_fDS(iter, preRenderViews, idx, i));
                            is_created[1] = true;
                            Threadlist[1].IsBackground = true;
                            Threadlist[1].Start();
                            //renderpart_fDS(iter, subcontainer, container);
                            break;
                        case 2:
                            Threadlist[2] = new Thread(() => renderpart_rDS(iter, preRenderViews, idx, i));
                            is_created[2] = true;
                            Threadlist[2].IsBackground = true;
                            Threadlist[2].Start();
                            //renderpart_rDS(iter, subcontainer, container);
                            break;
                        case 3:
                            Threadlist[3] = new Thread(() => renderpart_bDS(iter, preRenderViews, idx, i));
                            is_created[3] = true;
                            Threadlist[3].IsBackground = true;
                            Threadlist[3].Start();
                            //renderpart_bDS(iter, subcontainer, container);
                            break;
                    }
                }
                else if (digits[i] == 2)
                {
                    switch (i)
                    {
                        case 0:
                            Threadlist[0] = new Thread(() => renderpart_l(iter, preRenderViews, idx, i));
                            is_created[0] = true;
                            Threadlist[0].IsBackground = true;
                            Threadlist[0].Start();
                            //renderpart_l(iter, subcontainer, container);
                            break;
                        case 1:
                            Threadlist[1] = new Thread(() => renderpart_f(iter, preRenderViews, idx, i));
                            is_created[1] = true;
                            Threadlist[1].IsBackground = true;
                            Threadlist[1].Start();
                            break;
                        case 2:
                            Threadlist[2] = new Thread(() => renderpart_r(iter, preRenderViews, idx, i));
                            is_created[2] = true;
                            Threadlist[2].IsBackground = true;
                            Threadlist[2].Start();
                            break;
                        case 3:
                            Threadlist[3] = new Thread(() => renderpart_b(iter, preRenderViews, idx, i));
                            is_created[3] = true;
                            Threadlist[3].IsBackground = true;
                            Threadlist[3].Start();
                            break;
                    }
                    //renderthread.IsBackground = true;
                    //renderthread.Start();
                }
            }

            for (int i = 0; i < 4; i++)
            {
                if (is_created[i])
                {
                    UnityEngine.Debug.Log(i + " 번째 thread 종료 대기");
                    Threadlist[i].Join();
                    UnityEngine.Debug.Log(i + " 번째 thread 종료");
                }
            }
        }

        #region ESAS 전용 (10.27)
        public void renderpart_lDS(byte[] temp)
        {
            int temp_ = 0;
            temp_viewL = temp;
            int err = decoding(temp_viewL,
                          DSsubviewL,
                          temp_viewL.Length,
                          viewContainer.swidth,
                          viewContainer.sheight,
                          viewContainer.bpp);
            int threadnum = 8;
            resizelistL[0] = new Task(() =>
            {
                Resizing_Parall(DSsubviewL, decoded_viewL, DSsubviewL.Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 0, viewContainer.bpp);
            });
            resizelistL[0].Start();

            resizelistL[1] = new Task(() =>
            {
                Resizing_Parall(DSsubviewL, decoded_viewL, DSsubviewL.Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 1, viewContainer.bpp);
            });
            resizelistL[1].Start();

            resizelistL[2] = new Task(() =>
            {
                Resizing_Parall(DSsubviewL, decoded_viewL, DSsubviewL.Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 2, viewContainer.bpp);
            });
            resizelistL[2].Start();

            resizelistL[3] = new Task(() =>
            {
                Resizing_Parall(DSsubviewL, decoded_viewL, DSsubviewL.Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 3, viewContainer.bpp);
            });
            resizelistL[3].Start();

            resizelistL[4] = new Task(() =>
            {
                Resizing_Parall(DSsubviewL, decoded_viewL, DSsubviewL.Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 4, viewContainer.bpp);
            });
            resizelistL[4].Start();

            resizelistL[5] = new Task(() =>
            {
                Resizing_Parall(DSsubviewL, decoded_viewL, DSsubviewL.Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 5, viewContainer.bpp);
            });
            resizelistL[5].Start();

            resizelistL[6] = new Task(() =>
            {
                Resizing_Parall(DSsubviewL, decoded_viewL, DSsubviewL.Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 6, viewContainer.bpp);
            });
            resizelistL[6].Start();

            resizelistL[7] = new Task(() =>
            {
                Resizing_Parall(DSsubviewL, decoded_viewL, DSsubviewL.Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 7, viewContainer.bpp);
            });
            resizelistL[7].Start();

            //for (int i = 0; i < threadnum; i++)
            //{
            //    resizelistB[i].Start();
            //}

            for (int i = 0; i < threadnum; i++)
            {
                resizelistL[i].Wait();
            }
            viewContainer.setView(temp_, decoded_viewL);
        }
        public void renderpart_fDS(byte[] temp)
        {
            int temp_ = 1;
            temp_viewF = temp;
            int err = decoding(temp_viewF,
                          DSsubviewF,
                          temp_viewF.Length,
                          viewContainer.swidth,
                          viewContainer.sheight,
                          viewContainer.bpp);
            int threadnum = 8;
            resizelistF[0] = new Task(() =>
            {
                Resizing_Parall(DSsubviewF, decoded_viewF, DSsubviewF.Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 0, viewContainer.bpp);
            });
            resizelistF[0].Start();

            resizelistF[1] = new Task(() =>
            {
                Resizing_Parall(DSsubviewF, decoded_viewF, DSsubviewF.Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 1, viewContainer.bpp);
            });
            resizelistF[1].Start();

            resizelistF[2] = new Task(() =>
            {
                Resizing_Parall(DSsubviewF, decoded_viewF, DSsubviewF.Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 2, viewContainer.bpp);
            });
            resizelistF[2].Start();

            resizelistF[3] = new Task(() =>
            {
                Resizing_Parall(DSsubviewF, decoded_viewF, DSsubviewF.Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 3, viewContainer.bpp);
            });
            resizelistF[3].Start();

            resizelistF[4] = new Task(() =>
            {
                Resizing_Parall(DSsubviewF, decoded_viewF, DSsubviewF.Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 4, viewContainer.bpp);
            });
            resizelistF[4].Start();

            resizelistF[5] = new Task(() =>
            {
                Resizing_Parall(DSsubviewF, decoded_viewF, DSsubviewF.Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 5, viewContainer.bpp);
            });
            resizelistF[5].Start();

            resizelistF[6] = new Task(() =>
            {
                Resizing_Parall(DSsubviewF, decoded_viewF, DSsubviewF.Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 6, viewContainer.bpp);
            });
            resizelistF[6].Start();

            resizelistF[7] = new Task(() =>
            {
                Resizing_Parall(DSsubviewF, decoded_viewF, DSsubviewF.Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 7, viewContainer.bpp);
            });
            resizelistF[7].Start();

            //for (int i = 0; i < threadnum; i++)
            //{
            //    resizelistB[i].Start();
            //}

            for (int i = 0; i < threadnum; i++)
            {
                resizelistF[i].Wait();
            }
            viewContainer.setView(temp_, decoded_viewF);
        }
        public void renderpart_rDS(byte[] temp)
        {
            int temp_ = 2;
            temp_viewR = temp;
            int err = decoding(temp_viewR,
                          DSsubviewR,
                          temp_viewR.Length,
                          viewContainer.swidth,
                          viewContainer.sheight,
                          viewContainer.bpp);
            int threadnum = 8;
            resizelistR[0] = new Task(() =>
            {
                Resizing_Parall(DSsubviewR, decoded_viewR, DSsubviewR.Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 0, viewContainer.bpp);
            });
            resizelistR[0].Start();

            resizelistR[1] = new Task(() =>
            {
                Resizing_Parall(DSsubviewR, decoded_viewR, DSsubviewR.Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 1, viewContainer.bpp);
            });
            resizelistR[1].Start();

            resizelistR[2] = new Task(() =>
            {
                Resizing_Parall(DSsubviewR, decoded_viewR, DSsubviewR.Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 2, viewContainer.bpp);
            });
            resizelistR[2].Start();

            resizelistR[3] = new Task(() =>
            {
                Resizing_Parall(DSsubviewR, decoded_viewR, DSsubviewR.Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 3, viewContainer.bpp);
            });
            resizelistR[3].Start();

            resizelistR[4] = new Task(() =>
            {
                Resizing_Parall(DSsubviewR, decoded_viewR, DSsubviewR.Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 4, viewContainer.bpp);
            });
            resizelistR[4].Start();

            resizelistR[5] = new Task(() =>
            {
                Resizing_Parall(DSsubviewR, decoded_viewR, DSsubviewR.Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 5, viewContainer.bpp);
            });
            resizelistR[5].Start();

            resizelistR[6] = new Task(() =>
            {
                Resizing_Parall(DSsubviewR, decoded_viewR, DSsubviewR.Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 6, viewContainer.bpp);
            });
            resizelistR[6].Start();

            resizelistR[7] = new Task(() =>
            {
                Resizing_Parall(DSsubviewR, decoded_viewR, DSsubviewR.Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 7, viewContainer.bpp);
            });
            resizelistR[7].Start();

            //for (int i = 0; i < threadnum; i++)
            //{
            //    resizelistB[i].Start();
            //}

            for (int i = 0; i < threadnum; i++)
            {
                resizelistR[i].Wait();
            }
            viewContainer.setView(temp_, decoded_viewR);
        }
        public void renderpart_bDS(byte[] temp)
        {
            int temp_ = 3;
            temp_viewB = temp;
            int err = decoding(temp_viewB,
                          DSsubviewB,
                          temp_viewB.Length,
                          viewContainer.swidth,
                          viewContainer.sheight,
                          viewContainer.bpp);
            int threadnum = 8;
            resizelistB[0] = new Task(() =>
            {
                Resizing_Parall(DSsubviewB, decoded_viewB, DSsubviewB.Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 0, viewContainer.bpp);
            });
            resizelistB[0].Start();

            resizelistB[1] = new Task(() =>
            {
                Resizing_Parall(DSsubviewB, decoded_viewB, DSsubviewB.Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 1, viewContainer.bpp);
            });
            resizelistB[1].Start();

            resizelistB[2] = new Task(() =>
            {
                Resizing_Parall(DSsubviewB, decoded_viewB, DSsubviewB.Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 2, viewContainer.bpp);
            });
            resizelistB[2].Start();

            resizelistB[3] = new Task(() =>
            {
                Resizing_Parall(DSsubviewB, decoded_viewB, DSsubviewB.Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 3, viewContainer.bpp);
            });
            resizelistB[3].Start();

            resizelistB[4] = new Task(() =>
            {
                Resizing_Parall(DSsubviewB, decoded_viewB, DSsubviewB.Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 4, viewContainer.bpp);
            });
            resizelistB[4].Start();

            resizelistB[5] = new Task(() =>
            {
                Resizing_Parall(DSsubviewB, decoded_viewB, DSsubviewB.Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 5, viewContainer.bpp);
            });
            resizelistB[5].Start();

            resizelistB[6] = new Task(() =>
            {
                Resizing_Parall(DSsubviewB, decoded_viewB, DSsubviewB.Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 6, viewContainer.bpp);
            });
            resizelistB[6].Start();

            resizelistB[7] = new Task(() =>
            {
                Resizing_Parall(DSsubviewB, decoded_viewB, DSsubviewB.Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 7, viewContainer.bpp);
            });
            resizelistB[7].Start();

            //for (int i = 0; i < threadnum; i++)
            //{
            //    resizelistB[i].Start();
            //}

            for (int i = 0; i < threadnum; i++)
            {
                resizelistB[i].Wait();
            }
            viewContainer.setView(temp_, decoded_viewB);
        }

        public void renderpart_l(byte[] temp)
        {
            DateTime dec_time;
            temp_viewL = temp;
            dec_time = DateTime.Now;
            int err = decoding(temp_viewL,
                          decoded_viewL,
                          temp_viewL.Length,
                          viewContainer.owidth,
                          viewContainer.oheight,
                          viewContainer.bpp);
            double decodingtime = (DateTime.Now - dec_time).TotalMilliseconds;

            //UnityEngine.Debug.LogWarningFormat("left decoding time : {0:f4}", decodingtime);
            viewContainer.setView(0, decoded_viewL);
        }
        public void renderpart_f(byte[] temp)
        {
            temp_viewF = temp;
            //System.IO.File.WriteAllBytes(string.Format("output/{2} {5} front_{0}_{1} {3}_{4}.jpg", packet.pos.getX(), packet.pos.getY(), _time++, packet.loc.getPos_x(), packet.loc.getPos_y(), packet.loc.getPath()), temp_viewF);

            int err = decoding(temp_viewF,
                          decoded_viewF,
                          temp_viewF.Length,
                          viewContainer.owidth,
                          viewContainer.oheight,
                          viewContainer.bpp);
            viewContainer.setView(1, decoded_viewF);
        }
        public void renderpart_r(byte[] temp)
        {
            temp_viewR = temp;
            int err = decoding(temp_viewR,
                          decoded_viewR,
                          temp_viewR.Length,
                          viewContainer.owidth,
                          viewContainer.oheight,
                          viewContainer.bpp);
            viewContainer.setView(2, decoded_viewR);
        }
        public void renderpart_b(byte[] temp)
        {
            temp_viewB = temp;
            int err = decoding(temp_viewB,
                          decoded_viewB,
                          temp_viewB.Length,
                          viewContainer.owidth,
                          viewContainer.oheight,
                          viewContainer.bpp);
            viewContainer.setView(3, decoded_viewB);
        }


        #endregion

        #region Partial Rendering 관련 rendering 함수
        public void renderpart_lDS(byte[] temp, int iter)
        {
            int _iter = iter;
            int temp_ = 0;
            Ptemp_viewL[_iter] = temp;
            int err = decoding(Ptemp_viewL[_iter],
                          PDSsubviewL[_iter],
                          Ptemp_viewL[_iter].Length,
                          viewContainer.swidth,
                          viewContainer.sheight,
                          viewContainer.bpp);
            int threadnum = 8;
            resizelistL[0] = new Task(() =>
            {
                Resizing_Parall(PDSsubviewL[_iter], Pdecoded_viewL[_iter], PDSsubviewL[_iter].Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 0, viewContainer.bpp);
            });
            resizelistL[0].Start();

            resizelistL[1] = new Task(() =>
            {
                Resizing_Parall(PDSsubviewL[_iter], Pdecoded_viewL[_iter], PDSsubviewL[_iter].Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 1, viewContainer.bpp);
            });
            resizelistL[1].Start();

            resizelistL[2] = new Task(() =>
            {
                Resizing_Parall(PDSsubviewL[_iter], Pdecoded_viewL[_iter], PDSsubviewL[_iter].Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 2, viewContainer.bpp);
            });
            resizelistL[2].Start();

            resizelistL[3] = new Task(() =>
            {
                Resizing_Parall(PDSsubviewL[_iter], Pdecoded_viewL[_iter], PDSsubviewL[_iter].Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 3, viewContainer.bpp);
            });
            resizelistL[3].Start();

            resizelistL[4] = new Task(() =>
            {
                Resizing_Parall(PDSsubviewL[_iter], Pdecoded_viewL[_iter], PDSsubviewL[_iter].Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 4, viewContainer.bpp);
            });
            resizelistL[4].Start();

            resizelistL[5] = new Task(() =>
            {
                Resizing_Parall(PDSsubviewL[_iter], Pdecoded_viewL[_iter], PDSsubviewL[_iter].Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 5, viewContainer.bpp);
            });
            resizelistL[5].Start();

            resizelistL[6] = new Task(() =>
            {
                Resizing_Parall(PDSsubviewL[_iter], Pdecoded_viewL[_iter], PDSsubviewL[_iter].Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 6, viewContainer.bpp);
            });
            resizelistL[6].Start();

            resizelistL[7] = new Task(() =>
            {
                Resizing_Parall(PDSsubviewL[_iter], Pdecoded_viewL[_iter], PDSsubviewL[_iter].Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 7, viewContainer.bpp);
            });
            resizelistL[7].Start();

            //for (int i = 0; i < threadnum; i++)
            //{
            //    resizelistB[i].Start();
            //}

            for (int i = 0; i < threadnum; i++)
            {
                resizelistL[i].Wait();
            }
            viewContainers[_iter].setView(temp_, Pdecoded_viewL[_iter]);
        }
        public void renderpart_fDS(byte[] temp, int iter)
        {
            int _iter = iter;
            int temp_ = 1;
            Ptemp_viewF[_iter] = temp;
            int err = decoding(Ptemp_viewF[_iter],
                          PDSsubviewF[_iter],
                          Ptemp_viewF[_iter].Length,
                          viewContainer.swidth,
                          viewContainer.sheight,
                          viewContainer.bpp);
            int threadnum = 8;
            resizelistF[0] = new Task(() =>
            {
                Resizing_Parall(PDSsubviewF[_iter], Pdecoded_viewF[_iter], PDSsubviewF[_iter].Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 0, viewContainer.bpp);
            });
            resizelistF[0].Start();

            resizelistF[1] = new Task(() =>
            {
                Resizing_Parall(PDSsubviewF[_iter], Pdecoded_viewF[_iter], PDSsubviewF[_iter].Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 1, viewContainer.bpp);
            });
            resizelistF[1].Start();

            resizelistF[2] = new Task(() =>
            {
                Resizing_Parall(PDSsubviewF[_iter], Pdecoded_viewF[_iter], PDSsubviewF[_iter].Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 2, viewContainer.bpp);
            });
            resizelistF[2].Start();

            resizelistF[3] = new Task(() =>
            {
                Resizing_Parall(PDSsubviewF[_iter], Pdecoded_viewF[_iter], PDSsubviewF[_iter].Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 3, viewContainer.bpp);
            });
            resizelistF[3].Start();

            resizelistF[4] = new Task(() =>
            {
                Resizing_Parall(PDSsubviewF[_iter], Pdecoded_viewF[_iter], PDSsubviewF[_iter].Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 4, viewContainer.bpp);
            });
            resizelistF[4].Start();

            resizelistF[5] = new Task(() =>
            {
                Resizing_Parall(PDSsubviewF[_iter], Pdecoded_viewF[_iter], PDSsubviewF[_iter].Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 5, viewContainer.bpp);
            });
            resizelistF[5].Start();

            resizelistF[6] = new Task(() =>
            {
                Resizing_Parall(PDSsubviewF[_iter], Pdecoded_viewF[_iter], PDSsubviewF[_iter].Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 6, viewContainer.bpp);
            });
            resizelistF[6].Start();

            resizelistF[7] = new Task(() =>
            {
                Resizing_Parall(PDSsubviewF[_iter], Pdecoded_viewF[_iter], PDSsubviewF[_iter].Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 7, viewContainer.bpp);
            });
            resizelistF[7].Start();

            //for (int i = 0; i < threadnum; i++)
            //{
            //    resizelistB[i].Start();
            //}

            for (int i = 0; i < threadnum; i++)
            {
                resizelistF[i].Wait();
            }
            viewContainers[_iter].setView(temp_, Pdecoded_viewF[_iter]);
        }
        public void renderpart_rDS(byte[] temp, int iter)
        {
            int _iter = iter;
            int temp_ = 2;
            Ptemp_viewR[_iter] = temp;
            int err = decoding(Ptemp_viewR[_iter],
                          PDSsubviewR[_iter],
                          Ptemp_viewR[_iter].Length,
                          viewContainer.swidth,
                          viewContainer.sheight,
                          viewContainer.bpp);
            int threadnum = 8;
            resizelistR[0] = new Task(() =>
            {
                Resizing_Parall(PDSsubviewR[_iter], Pdecoded_viewR[_iter], PDSsubviewR[_iter].Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 0, viewContainer.bpp);
            });
            resizelistR[0].Start();

            resizelistR[1] = new Task(() =>
            {
                Resizing_Parall(PDSsubviewR[_iter], Pdecoded_viewR[_iter], PDSsubviewR[_iter].Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 1, viewContainer.bpp);
            });
            resizelistR[1].Start();

            resizelistR[2] = new Task(() =>
            {
                Resizing_Parall(PDSsubviewR[_iter], Pdecoded_viewR[_iter], PDSsubviewR[_iter].Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 2, viewContainer.bpp);
            });
            resizelistR[2].Start();

            resizelistR[3] = new Task(() =>
            {
                Resizing_Parall(PDSsubviewR[_iter], Pdecoded_viewR[_iter], PDSsubviewR[_iter].Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 3, viewContainer.bpp);
            });
            resizelistR[3].Start();

            resizelistR[4] = new Task(() =>
            {
                Resizing_Parall(PDSsubviewR[_iter], Pdecoded_viewR[_iter], PDSsubviewR[_iter].Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 4, viewContainer.bpp);
            });
            resizelistR[4].Start();

            resizelistR[5] = new Task(() =>
            {
                Resizing_Parall(PDSsubviewR[_iter], Pdecoded_viewR[_iter], PDSsubviewR[_iter].Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 5, viewContainer.bpp);
            });
            resizelistR[5].Start();

            resizelistR[6] = new Task(() =>
            {
                Resizing_Parall(PDSsubviewR[_iter], Pdecoded_viewR[_iter], PDSsubviewR[_iter].Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 6, viewContainer.bpp);
            });
            resizelistR[6].Start();

            resizelistR[7] = new Task(() =>
            {
                Resizing_Parall(PDSsubviewR[_iter], Pdecoded_viewR[_iter], PDSsubviewR[_iter].Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 7, viewContainer.bpp);
            });
            resizelistR[7].Start();

            //for (int i = 0; i < threadnum; i++)
            //{
            //    resizelistB[i].Start();
            //}

            for (int i = 0; i < threadnum; i++)
            {
                resizelistR[i].Wait();
            }
            viewContainers[_iter].setView(temp_, Pdecoded_viewR[_iter]);
        }
        public void renderpart_bDS(byte[] temp, int iter)
        {
            int _iter = iter;
            int temp_ = 3;
            Ptemp_viewB[_iter] = temp;
            int err = decoding(Ptemp_viewB[_iter],
                          PDSsubviewB[_iter],
                          Ptemp_viewB[_iter].Length,
                          viewContainer.swidth,
                          viewContainer.sheight,
                          viewContainer.bpp);
            int threadnum = 8;
            resizelistB[0] = new Task(() =>
            {
                Resizing_Parall(PDSsubviewB[_iter], Pdecoded_viewB[_iter], PDSsubviewB[_iter].Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 0, viewContainer.bpp);
            });
            resizelistB[0].Start();

            resizelistB[1] = new Task(() =>
            {
                Resizing_Parall(PDSsubviewB[_iter], Pdecoded_viewB[_iter], PDSsubviewB[_iter].Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 1, viewContainer.bpp);
            });
            resizelistB[1].Start();

            resizelistB[2] = new Task(() =>
            {
                Resizing_Parall(PDSsubviewB[_iter], Pdecoded_viewB[_iter], PDSsubviewB[_iter].Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 2, viewContainer.bpp);
            });
            resizelistB[2].Start();

            resizelistB[3] = new Task(() =>
            {
                Resizing_Parall(PDSsubviewB[_iter], Pdecoded_viewB[_iter], PDSsubviewB[_iter].Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 3, viewContainer.bpp);
            });
            resizelistB[3].Start();

            resizelistB[4] = new Task(() =>
            {
                Resizing_Parall(PDSsubviewB[_iter], Pdecoded_viewB[_iter], PDSsubviewB[_iter].Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 4, viewContainer.bpp);
            });
            resizelistB[4].Start();

            resizelistB[5] = new Task(() =>
            {
                Resizing_Parall(PDSsubviewB[_iter], Pdecoded_viewB[_iter], PDSsubviewB[_iter].Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 5, viewContainer.bpp);
            });
            resizelistB[5].Start();

            resizelistB[6] = new Task(() =>
            {
                Resizing_Parall(PDSsubviewB[_iter], Pdecoded_viewB[_iter], PDSsubviewB[_iter].Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 6, viewContainer.bpp);
            });
            resizelistB[6].Start();

            resizelistB[7] = new Task(() =>
            {
                Resizing_Parall(PDSsubviewB[_iter], Pdecoded_viewB[_iter], PDSsubviewB[_iter].Length, viewContainer.swidth, viewContainer.sheight, viewContainer.owidth, viewContainer.oheight, threadnum, 7, viewContainer.bpp);
            });
            resizelistB[7].Start();

            //for (int i = 0; i < threadnum; i++)
            //{
            //    resizelistB[i].Start();
            //}

            for (int i = 0; i < threadnum; i++)
            {
                resizelistB[i].Wait();
            }
            viewContainers[_iter].setView(temp_, Pdecoded_viewB[_iter]);
        }

        public void renderpart_l(byte[] temp, int iter)
        {
            int _iter = iter;
            DateTime dec_time;
            Ptemp_viewL[_iter] = temp;
            dec_time = DateTime.Now;
            int err = decoding(Ptemp_viewL[_iter],
                          Pdecoded_viewL[_iter],
                          Ptemp_viewL[_iter].Length,
                          viewContainer.owidth,
                          viewContainer.oheight,
                          viewContainer.bpp);
            double decodingtime = (DateTime.Now - dec_time).TotalMilliseconds;

            //UnityEngine.Debug.LogWarningFormat("left decoding time : {0:f4}", decodingtime);
            //UnityEngine.Debug.LogErrorFormat("{0} 번째 {2} sub view : {1}", _iter, temp.Length, 0);

            viewContainers[_iter].setView(0, Pdecoded_viewL[_iter]);
        }
        public void renderpart_f(byte[] temp, int iter)
        {
            int _iter = iter;
            DateTime dec_time;
            Ptemp_viewF[_iter] = temp;
            dec_time = DateTime.Now;
            int err = decoding(Ptemp_viewF[_iter],
                          Pdecoded_viewF[_iter],
                          Ptemp_viewF[_iter].Length,
                          viewContainer.owidth,
                          viewContainer.oheight,
                          viewContainer.bpp);
            double decodingtime = (DateTime.Now - dec_time).TotalMilliseconds;

            //UnityEngine.Debug.LogWarningFormat("left decoding time : {0:f4}", decodingtime);

            viewContainers[_iter].setView(1, Pdecoded_viewF[_iter]);
        }
        public void renderpart_r(byte[] temp, int iter)
        {
            int _iter = iter;
            DateTime dec_time;
            Ptemp_viewR[_iter] = temp;
            dec_time = DateTime.Now;
            int err = decoding(Ptemp_viewR[_iter],
                          Pdecoded_viewR[_iter],
                          Ptemp_viewR[_iter].Length,
                          viewContainer.owidth,
                          viewContainer.oheight,
                          viewContainer.bpp);
            double decodingtime = (DateTime.Now - dec_time).TotalMilliseconds;

            //UnityEngine.Debug.LogWarningFormat("left decoding time : {0:f4}", decodingtime);

            viewContainers[_iter].setView(2, Pdecoded_viewR[_iter]);
        }
        public void renderpart_b(byte[] temp, int iter)
        {
            int _iter = iter;
            DateTime dec_time;
            Ptemp_viewB[_iter] = temp;
            dec_time = DateTime.Now;
            int err = decoding(Ptemp_viewB[_iter],
                          Pdecoded_viewB[_iter],
                          Ptemp_viewB[_iter].Length,
                          viewContainer.owidth,
                          viewContainer.oheight,
                          viewContainer.bpp);
            double decodingtime = (DateTime.Now - dec_time).TotalMilliseconds;

            //UnityEngine.Debug.LogWarningFormat("left decoding time : {0:f4}", decodingtime);

            viewContainers[_iter].setView(3, Pdecoded_viewB[_iter]);
        }
        #endregion



        public void renderpart_l(int iter, pre_renderViews container, int idx, int digit)
        {
            DateTime dec_time;
            int temp_ = 0;
            int _iter = iter;
            temp_viewL = cache.GetView(idx, _iter, temp_);
            dec_time = DateTime.Now;
            int err = decoding(temp_viewL,
                          decoded_viewL,
                          temp_viewL.Length,
                          container.owidth,
                          container.oheight,
                          container.bpp);
            double decodingtime = (DateTime.Now - dec_time).TotalMilliseconds;

            //UnityEngine.Debug.LogWarningFormat("left decoding time : {0:f4}", decodingtime);
            container.setView(temp_, decoded_viewL, _iter);
        }
        public void renderpart_f(int iter, pre_renderViews container, int idx, int digit)
        {
            int temp_ = 1;
            int _iter = iter;
            temp_viewF = cache.GetView(idx, _iter, temp_);
            //System.IO.File.WriteAllBytes(string.Format("output/{2} {5} front_{0}_{1} {3}_{4}.jpg", packet.pos.getX(), packet.pos.getY(), _time++, packet.loc.getPos_x(), packet.loc.getPos_y(), packet.loc.getPath()), temp_viewF);

            int err = decoding(temp_viewF,
                          decoded_viewF,
                          temp_viewF.Length,
                          container.owidth,
                          container.oheight,
                          container.bpp);
            container.setView(temp_, decoded_viewF, _iter);
        }
        public void renderpart_r(int iter, pre_renderViews container, int idx, int digit)
        {
            int temp_ = 2;
            int _iter = iter;
            UnityEngine.Debug.LogErrorFormat("cache view size : {0} idx : {1} digit : {2}", temp_viewR.Length, idx, temp_);
            temp_viewR = cache.GetView(idx, _iter, temp_);
            int err = decoding(temp_viewR,
                          decoded_viewR,
                          temp_viewR.Length,
                          container.owidth,
                          container.oheight,
                          container.bpp);
            container.setView(temp_, decoded_viewR, _iter);
        }
        public void renderpart_b(int iter, pre_renderViews container, int idx, int digit)
        {
            int temp_ = 3;
            int _iter = iter;
            temp_viewB = cache.GetView(idx, _iter, temp_);
            int err = decoding(temp_viewB,
                          decoded_viewB,
                          temp_viewB.Length,
                          container.owidth,
                          container.oheight,
                          container.bpp);
            container.setView(temp_, decoded_viewB, _iter);
        }
        public void renderpart_lDS(int iter, pre_renderViews container, int idx, int digit)
        {
            DateTime dec_time;
            DateTime resize_time;
            int temp_ = 0;
            int _iter = iter;
            temp_viewL = cache.GetView(idx, _iter, temp_);
            dec_time = DateTime.Now;
            int err = decoding(temp_viewL,
                          DSsubviewL,
                          temp_viewL.Length,
                          container.swidth,
                          container.sheight,
                          container.bpp);
            double decodingtime = (DateTime.Now - dec_time).TotalMilliseconds;
            resize_time = DateTime.Now;
            int threadnum = 8;
            resizelistL[0] = new Task(() =>
            {
                Resizing_Parall(DSsubviewL, decoded_viewL, DSsubviewL.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 0, container.bpp);
            });
            resizelistL[0].Start();

            resizelistL[1] = new Task(() =>
            {
                Resizing_Parall(DSsubviewL, decoded_viewL, DSsubviewL.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 1, container.bpp);
            });
            resizelistL[1].Start();

            resizelistL[2] = new Task(() =>
            {
                Resizing_Parall(DSsubviewL, decoded_viewL, DSsubviewL.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 2, container.bpp);
            });
            resizelistL[2].Start();

            resizelistL[3] = new Task(() =>
            {
                Resizing_Parall(DSsubviewL, decoded_viewL, DSsubviewL.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 3, container.bpp);
            });
            resizelistL[3].Start();

            resizelistL[4] = new Task(() =>
            {
                Resizing_Parall(DSsubviewL, decoded_viewL, DSsubviewL.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 4, container.bpp);
            });
            resizelistL[4].Start();

            resizelistL[5] = new Task(() =>
            {
                Resizing_Parall(DSsubviewL, decoded_viewL, DSsubviewL.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 5, container.bpp);
            });
            resizelistL[5].Start();

            resizelistL[6] = new Task(() =>
            {
                Resizing_Parall(DSsubviewL, decoded_viewL, DSsubviewL.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 6, container.bpp);
            });
            resizelistL[6].Start();

            resizelistL[7] = new Task(() =>
            {
                Resizing_Parall(DSsubviewL, decoded_viewL, DSsubviewL.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 7, container.bpp);
            });
            resizelistL[7].Start();

            //for (int i = 0; i < threadnum; i++)
            //{
            //    resizelistL[i].Start();
            //}

            for (int i = 0; i < threadnum; i++)
            {
                resizelistL[i].Wait();
            }
            double resizingtime = (DateTime.Now - resize_time).TotalMilliseconds;
            UnityEngine.Debug.LogWarningFormat("left decoding : {0:f4} ms, resizing : {1:f4} ms", decodingtime, resizingtime);

            container.setView(temp_, decoded_viewL, _iter);
        }
        public void renderpart_fDS(int iter, pre_renderViews container, int idx, int digit)
        {
            int temp_ = 1;
            int _iter = iter;
            temp_viewF = cache.GetView(idx, _iter, temp_);
            int err = decoding(temp_viewF,
                          DSsubviewF,
                          temp_viewF.Length,
                          container.swidth,
                          container.sheight,
                          container.bpp);
            int threadnum = 8;
            resizelistF[0] = new Task(() =>
            {
                Resizing_Parall(DSsubviewF, decoded_viewF, DSsubviewF.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 0, container.bpp);
            });
            resizelistF[0].Start();

            resizelistF[1] = new Task(() =>
            {
                Resizing_Parall(DSsubviewF, decoded_viewF, DSsubviewF.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 1, container.bpp);
            });
            resizelistF[1].Start();

            resizelistF[2] = new Task(() =>
            {
                Resizing_Parall(DSsubviewF, decoded_viewF, DSsubviewF.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 2, container.bpp);
            });
            resizelistF[2].Start();

            resizelistF[3] = new Task(() =>
            {
                Resizing_Parall(DSsubviewF, decoded_viewF, DSsubviewF.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 3, container.bpp);
            });
            resizelistF[3].Start();

            resizelistF[4] = new Task(() =>
            {
                Resizing_Parall(DSsubviewF, decoded_viewF, DSsubviewF.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 4, container.bpp);
            });
            resizelistF[4].Start();

            resizelistF[5] = new Task(() =>
            {
                Resizing_Parall(DSsubviewF, decoded_viewF, DSsubviewF.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 5, container.bpp);
            });
            resizelistF[5].Start();

            resizelistF[6] = new Task(() =>
            {
                Resizing_Parall(DSsubviewF, decoded_viewF, DSsubviewF.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 6, container.bpp);
            });
            resizelistF[6].Start();

            resizelistF[7] = new Task(() =>
            {
                Resizing_Parall(DSsubviewF, decoded_viewF, DSsubviewF.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 7, container.bpp);
            });
            resizelistF[7].Start();

            //for (int i = 0; i < threadnum; i++)
            //{
            //    resizelistF[i].Start();
            //}

            for (int i = 0; i < threadnum; i++)
            {
                resizelistF[i].Wait();
            }

            container.setView(temp_, decoded_viewF, _iter);
        }
        public void renderpart_rDS(int iter, pre_renderViews container, int idx, int digit)
        {
            int temp_ = 2;
            int _iter = iter;
            temp_viewR = cache.GetView(idx, iter, temp_);
            int err = decoding(temp_viewR,
                          DSsubviewR,
                          temp_viewR.Length,
                          container.swidth,
                          container.sheight,
                          container.bpp);
            int threadnum = 8;
            resizelistR[0] = new Task(() =>
            {
                Resizing_Parall(DSsubviewR, decoded_viewR, DSsubviewR.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 0, container.bpp);
            });
            resizelistR[0].Start();

            resizelistR[1] = new Task(() =>
            {
                Resizing_Parall(DSsubviewR, decoded_viewR, DSsubviewR.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 1, container.bpp);
            });
            resizelistR[1].Start();

            resizelistR[2] = new Task(() =>
            {
                Resizing_Parall(DSsubviewR, decoded_viewR, DSsubviewR.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 2, container.bpp);
            });
            resizelistR[2].Start();

            resizelistR[3] = new Task(() =>
            {
                Resizing_Parall(DSsubviewR, decoded_viewR, DSsubviewR.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 3, container.bpp);
            });
            resizelistR[3].Start();

            resizelistR[4] = new Task(() =>
            {
                Resizing_Parall(DSsubviewR, decoded_viewR, DSsubviewR.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 4, container.bpp);
            });
            resizelistR[4].Start();

            resizelistR[5] = new Task(() =>
            {
                Resizing_Parall(DSsubviewR, decoded_viewR, DSsubviewR.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 5, container.bpp);
            });
            resizelistR[5].Start();

            resizelistR[6] = new Task(() =>
            {
                Resizing_Parall(DSsubviewR, decoded_viewR, DSsubviewR.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 6, container.bpp);
            });
            resizelistR[6].Start();

            resizelistR[7] = new Task(() =>
            {
                Resizing_Parall(DSsubviewR, decoded_viewR, DSsubviewR.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 7, container.bpp);
            });
            resizelistR[7].Start();


            //for (int i = 0; i < threadnum; i++)
            //{
            //    resizelistR[i].Start();
            //}

            for (int i = 0; i < threadnum; i++)
            {
                resizelistR[i].Wait();
            }
            container.setView(temp_, decoded_viewR, _iter);
        }
        public void renderpart_bDS(int iter, pre_renderViews container, int idx, int digit)
        {
            int temp_ = 3;
            int _iter = iter;
            temp_viewB = cache.GetView(idx, _iter, temp_);
            int err = decoding(temp_viewB,
                          DSsubviewB,
                          temp_viewB.Length,
                          container.swidth,
                          container.sheight,
                          container.bpp);
            int threadnum = 8;
            resizelistB[0] = new Task(() =>
            {
                Resizing_Parall(DSsubviewB, decoded_viewB, DSsubviewB.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 0, container.bpp);
            });
            resizelistB[0].Start();

            resizelistB[1] = new Task(() =>
            {
                Resizing_Parall(DSsubviewB, decoded_viewB, DSsubviewB.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 1, container.bpp);
            });
            resizelistB[1].Start();

            resizelistB[2] = new Task(() =>
            {
                Resizing_Parall(DSsubviewB, decoded_viewB, DSsubviewB.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 2, container.bpp);
            });
            resizelistB[2].Start();

            resizelistB[3] = new Task(() =>
            {
                Resizing_Parall(DSsubviewB, decoded_viewB, DSsubviewB.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 3, container.bpp);
            });
            resizelistB[3].Start();

            resizelistB[4] = new Task(() =>
            {
                Resizing_Parall(DSsubviewB, decoded_viewB, DSsubviewB.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 4, container.bpp);
            });
            resizelistB[4].Start();

            resizelistB[5] = new Task(() =>
            {
                Resizing_Parall(DSsubviewB, decoded_viewB, DSsubviewB.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 5, container.bpp);
            });
            resizelistB[5].Start();

            resizelistB[6] = new Task(() =>
            {
                Resizing_Parall(DSsubviewB, decoded_viewB, DSsubviewB.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 6, container.bpp);
            });
            resizelistB[6].Start();

            resizelistB[7] = new Task(() =>
            {
                Resizing_Parall(DSsubviewB, decoded_viewB, DSsubviewB.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 7, container.bpp);
            });
            resizelistB[7].Start();

            //for (int i = 0; i < threadnum; i++)
            //{
            //    resizelistB[i].Start();
            //}

            for (int i = 0; i < threadnum; i++)
            {
                resizelistB[i].Wait();
            }
            container.setView(temp_, decoded_viewB, _iter);
        }

        public void onlydecoding_DS(int iter, pre_renderViews container, int idx, int digit)
        {
            int temp_ = 0;
            int _iter = iter;
            temp_viewOnlyDec = cache.GetView(idx, iter, digit);
            int err = decoding(temp_viewOnlyDec,
                          DSsubviewOnlyDec,
                          temp_viewOnlyDec.Length,
                          container.swidth,
                          container.sheight,
                          container.bpp);
            int threadnum = 8;
            Task[] resizelist = new Task[threadnum];
            resizelist[0] = new Task(() =>
            {
                Resizing_Parall(DSsubviewOnlyDec, decoded_viewOnlyDec, DSsubviewOnlyDec.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 0, container.bpp);
            });

            resizelist[1] = new Task(() =>
            {
                Resizing_Parall(DSsubviewOnlyDec, decoded_viewOnlyDec, DSsubviewOnlyDec.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 1, container.bpp);
            });

            resizelist[2] = new Task(() =>
            {
                Resizing_Parall(DSsubviewOnlyDec, decoded_viewOnlyDec, DSsubviewOnlyDec.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 2, container.bpp);
            });

            resizelist[3] = new Task(() =>
            {
                Resizing_Parall(DSsubviewOnlyDec, decoded_viewOnlyDec, DSsubviewOnlyDec.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 3, container.bpp);
            });
            resizelist[4] = new Task(() =>
            {
                Resizing_Parall(DSsubviewOnlyDec, decoded_viewOnlyDec, DSsubviewOnlyDec.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 4, container.bpp);
            });

            resizelist[5] = new Task(() =>
            {
                Resizing_Parall(DSsubviewOnlyDec, decoded_viewOnlyDec, DSsubviewOnlyDec.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 5, container.bpp);
            });

            resizelist[6] = new Task(() =>
            {
                Resizing_Parall(DSsubviewOnlyDec, decoded_viewOnlyDec, DSsubviewOnlyDec.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 6, container.bpp);
            });

            resizelist[7] = new Task(() =>
            {
                Resizing_Parall(DSsubviewOnlyDec, decoded_viewOnlyDec, DSsubviewOnlyDec.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 7, container.bpp);
            });


            for (int i = 0; i < threadnum; i++)
            {
                resizelist[i].Start();
            }

            for (int i = 0; i < threadnum; i++)
            {
                resizelist[i].Wait();
            }
            container.setView(temp_, decoded_viewOnlyDec, iter);
        }
        public void onlydecoding(int iter, pre_renderViews container, int idx, int digit)
        {
            int temp_ = 0;
            int _iter = iter;
            temp_viewOnlyDec = cache.GetView(idx, iter, digit);
            int err = decoding(temp_viewOnlyDec,
                          decoded_viewOnlyDec,
                          temp_viewOnlyDec.Length,
                          container.swidth,
                          container.sheight,
                          container.bpp);
            container.setView(temp_, decoded_viewOnlyDec, iter);
        }




        public void renderpart_l(int iter, jpeg_container jpegcontainer, view_container container)
        {
            DateTime dec_time;
            int _iter = iter;
            temp_viewL = jpegcontainer.getView(_iter);
            dec_time = DateTime.Now;
            int err = decoding(temp_viewL,
                          decoded_viewL,
                          temp_viewL.Length,
                          container.owidth,
                          container.oheight,
                          container.bpp);
            double decodingtime = (DateTime.Now - dec_time).TotalMilliseconds;

            //UnityEngine.Debug.LogWarningFormat("left decoding time : {0:f4}", decodingtime);
            container.setView(_iter, decoded_viewL);
        }
        public void renderpart_f(int iter, jpeg_container jpegcontainer, view_container container, RequestPacket packet)
        {
            int _iter = iter;
            temp_viewF = jpegcontainer.getView(_iter);
            //System.IO.File.WriteAllBytes(string.Format("output/{2} {5} front_{0}_{1} {3}_{4}.jpg", packet.pos.getX(), packet.pos.getY(), _time++, packet.loc.getPos_x(), packet.loc.getPos_y(), packet.loc.getPath()), temp_viewF);

            int err = decoding(temp_viewF,
                          decoded_viewF,
                          temp_viewF.Length,
                          container.owidth,
                          container.oheight,
                          container.bpp);
            container.setView(_iter, decoded_viewF);
        }
        public void renderpart_r(int iter, jpeg_container jpegcontainer, view_container container)
        {
            int _iter = iter;
            temp_viewR = jpegcontainer.getView(_iter);
            int err = decoding(temp_viewR,
                          decoded_viewR,
                          temp_viewR.Length,
                          container.owidth,
                          container.oheight,
                          container.bpp);
            container.setView(_iter, decoded_viewR);
        }
        public void renderpart_b(int iter, jpeg_container jpegcontainer, view_container container)
        {
            int _iter = iter;
            temp_viewB = jpegcontainer.getView(_iter);
            int err = decoding(temp_viewB,
                          decoded_viewB,
                          temp_viewB.Length,
                          container.owidth,
                          container.oheight,
                          container.bpp);
            container.setView(_iter, decoded_viewB);
        }


        public void renderpart_lDS(int iter, jpeg_container jpegcontainer, view_container container)
        {
            int _iter = iter;
            temp_viewL = jpegcontainer.getView(_iter);
            int err = decoding(temp_viewL,
                          DSsubviewL,
                          temp_viewL.Length,
                          container.swidth,
                          container.sheight,
                          container.bpp);
            int threadnum = 8;
            Task[] resizelist = new Task[threadnum];
            resizelist[0] = new Task(() =>
            {
                Resizing_Parall(DSsubviewL, decoded_viewL, DSsubviewL.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 0, container.bpp);
            });

            resizelist[1] = new Task(() =>
            {
                Resizing_Parall(DSsubviewL, decoded_viewL, DSsubviewL.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 1, container.bpp);
            });

            resizelist[2] = new Task(() =>
            {
                Resizing_Parall(DSsubviewL, decoded_viewL, DSsubviewL.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 2, container.bpp);
            });

            resizelist[3] = new Task(() =>
            {
                Resizing_Parall(DSsubviewL, decoded_viewL, DSsubviewL.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 3, container.bpp);
            });
            resizelist[4] = new Task(() =>
            {
                Resizing_Parall(DSsubviewL, decoded_viewL, DSsubviewL.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 4, container.bpp);
            });

            resizelist[5] = new Task(() =>
            {
                Resizing_Parall(DSsubviewL, decoded_viewL, DSsubviewL.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 5, container.bpp);
            });

            resizelist[6] = new Task(() =>
            {
                Resizing_Parall(DSsubviewL, decoded_viewL, DSsubviewL.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 6, container.bpp);
            });

            resizelist[7] = new Task(() =>
            {
                Resizing_Parall(DSsubviewL, decoded_viewL, DSsubviewL.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 7, container.bpp);
            });


            for (int i = 0; i < threadnum; i++)
            {
                resizelist[i].Start();
            }

            for (int i = 0; i < threadnum; i++)
            {
                resizelist[i].Wait();
            }
            container.setView(_iter, decoded_viewL);
        }
        public void renderpart_fDS(int iter, jpeg_container jpegcontainer, view_container container)
        {
            int _iter = iter;
            temp_viewF = jpegcontainer.getView(_iter);
            int err = decoding(temp_viewF,
                          DSsubviewF,
                          temp_viewF.Length,
                          container.swidth,
                          container.sheight,
                          container.bpp);
            int threadnum = 8;
            Task[] resizelist = new Task[threadnum];
            resizelist[0] = new Task(() =>
            {
                Resizing_Parall(DSsubviewF, decoded_viewF, DSsubviewF.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 0, container.bpp);
            });

            resizelist[1] = new Task(() =>
            {
                Resizing_Parall(DSsubviewF, decoded_viewF, DSsubviewF.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 1, container.bpp);
            });

            resizelist[2] = new Task(() =>
            {
                Resizing_Parall(DSsubviewF, decoded_viewF, DSsubviewF.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 2, container.bpp);
            });

            resizelist[3] = new Task(() =>
            {
                Resizing_Parall(DSsubviewF, decoded_viewF, DSsubviewF.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 3, container.bpp);
            });
            resizelist[4] = new Task(() =>
            {
                Resizing_Parall(DSsubviewF, decoded_viewF, DSsubviewF.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 4, container.bpp);
            });

            resizelist[5] = new Task(() =>
            {
                Resizing_Parall(DSsubviewF, decoded_viewF, DSsubviewF.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 5, container.bpp);
            });

            resizelist[6] = new Task(() =>
            {
                Resizing_Parall(DSsubviewF, decoded_viewF, DSsubviewF.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 6, container.bpp);
            });

            resizelist[7] = new Task(() =>
            {
                Resizing_Parall(DSsubviewF, decoded_viewF, DSsubviewF.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 7, container.bpp);
            });


            for (int i = 0; i < threadnum; i++)
            {
                resizelist[i].Start();
            }

            for (int i = 0; i < threadnum; i++)
            {
                resizelist[i].Wait();
            }

            container.setView(_iter, decoded_viewF);
        }
        public void renderpart_rDS(int iter, jpeg_container jpegcontainer, view_container container)
        {
            int _iter = iter;
            temp_viewR = jpegcontainer.getView(_iter);
            int err = decoding(temp_viewR,
                          DSsubviewR,
                          temp_viewR.Length,
                          container.swidth,
                          container.sheight,
                          container.bpp);
            int threadnum = 8;
            Task[] resizelist = new Task[threadnum];
            resizelist[0] = new Task(() =>
            {
                Resizing_Parall(DSsubviewR, decoded_viewR, DSsubviewR.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 0, container.bpp);
            });

            resizelist[1] = new Task(() =>
            {
                Resizing_Parall(DSsubviewR, decoded_viewR, DSsubviewR.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 1, container.bpp);
            });

            resizelist[2] = new Task(() =>
            {
                Resizing_Parall(DSsubviewR, decoded_viewR, DSsubviewR.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 2, container.bpp);
            });

            resizelist[3] = new Task(() =>
            {
                Resizing_Parall(DSsubviewR, decoded_viewR, DSsubviewR.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 3, container.bpp);
            });
            resizelist[4] = new Task(() =>
            {
                Resizing_Parall(DSsubviewR, decoded_viewR, DSsubviewR.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 4, container.bpp);
            });

            resizelist[5] = new Task(() =>
            {
                Resizing_Parall(DSsubviewR, decoded_viewR, DSsubviewR.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 5, container.bpp);
            });

            resizelist[6] = new Task(() =>
            {
                Resizing_Parall(DSsubviewR, decoded_viewR, DSsubviewR.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 6, container.bpp);
            });

            resizelist[7] = new Task(() =>
            {
                Resizing_Parall(DSsubviewR, decoded_viewR, DSsubviewR.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 7, container.bpp);
            });


            for (int i = 0; i < threadnum; i++)
            {
                resizelist[i].Start();
            }

            for (int i = 0; i < threadnum; i++)
            {
                resizelist[i].Wait();
            }
            container.setView(_iter, decoded_viewR);
        }
        public void renderpart_bDS(int iter, jpeg_container jpegcontainer, view_container container)
        {
            int _iter = iter;
            temp_viewB = jpegcontainer.getView(_iter);
            int err = decoding(temp_viewB,
                          DSsubviewB,
                          temp_viewB.Length,
                          container.swidth,
                          container.sheight,
                          container.bpp);
            int threadnum = 8;
            Task[] resizelist = new Task[threadnum];
            resizelist[0] = new Task(() =>
            {
                Resizing_Parall(DSsubviewB, decoded_viewB, DSsubviewB.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 0, container.bpp);
            });

            resizelist[1] = new Task(() =>
            {
                Resizing_Parall(DSsubviewB, decoded_viewB, DSsubviewB.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 1, container.bpp);
            });

            resizelist[2] = new Task(() =>
            {
                Resizing_Parall(DSsubviewB, decoded_viewB, DSsubviewB.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 2, container.bpp);
            });

            resizelist[3] = new Task(() =>
            {
                Resizing_Parall(DSsubviewB, decoded_viewB, DSsubviewB.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 3, container.bpp);
            });
            resizelist[4] = new Task(() =>
            {
                Resizing_Parall(DSsubviewB, decoded_viewB, DSsubviewB.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 4, container.bpp);
            });

            resizelist[5] = new Task(() =>
            {
                Resizing_Parall(DSsubviewB, decoded_viewB, DSsubviewB.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 5, container.bpp);
            });

            resizelist[6] = new Task(() =>
            {
                Resizing_Parall(DSsubviewB, decoded_viewB, DSsubviewB.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 6, container.bpp);
            });

            resizelist[7] = new Task(() =>
            {
                Resizing_Parall(DSsubviewB, decoded_viewB, DSsubviewB.Length, container.swidth, container.sheight, container.owidth, container.oheight, threadnum, 7, container.bpp);
            });


            for (int i = 0; i < threadnum; i++)
            {
                resizelist[i].Start();
            }

            for (int i = 0; i < threadnum; i++)
            {
                resizelist[i].Wait();
            }
            container.setView(_iter, decoded_viewB);
        }


        /*
        public void renderpart_lDS(int iter, subseg_container subcontainer, view_container container)
        {
            DateTime dec_time;
            DateTime resize_time;
            int temp_ = 0;
            int _iter = iter;
            temp_viewL = subcontainer.getView(temp_, _iter);
            dec_time = DateTime.Now;
            int err = decoding(temp_viewL,
                          DSsubviewL,
                          temp_viewL.Length,
                          container.swidth,
                          container.sheight,
                          container.bpp);
            double decodingtime = (DateTime.Now - dec_time).TotalMilliseconds;
            resize_time = DateTime.Now;
            Resize_Slite(DSsubviewL,
                   decoded_viewL,
                   DSsubviewL.Length,
                   container.swidth,
                   container.sheight,
                   container.owidth,
                   container.oheight);
            double resizingtime = (DateTime.Now - resize_time).TotalMilliseconds;
            UnityEngine.Debug.LogWarningFormat("left decoding : {0:f4} ms, resizing : {1:f4} ms", decodingtime, resizingtime);

            container.setView(temp_, decoded_viewL);
        }
        public void renderpart_fDS(int iter, subseg_container subcontainer, view_container container)
        {
            int temp_ = 1;
            int _iter = iter;
            temp_viewF = subcontainer.getView(temp_, _iter);
            int err = decoding(temp_viewF,
                          DSsubviewF,
                          temp_viewF.Length,
                          container.swidth,
                          container.sheight,
                          container.bpp);
            Resize_Slite(DSsubviewF,
                   decoded_viewF,
                   DSsubviewF.Length,
                   container.swidth,
                   container.sheight,
                   container.owidth,
                   container.oheight);
            container.setView(temp_, decoded_viewF);
        }
        public void renderpart_rDS(int iter, subseg_container subcontainer, view_container container)
        {
            int temp_ = 2;
            int _iter = iter;
            temp_viewR = subcontainer.getView(temp_, _iter);
            int err = decoding(temp_viewR,
                          DSsubviewR,
                          temp_viewR.Length,
                          container.swidth,
                          container.sheight,
                          container.bpp);
            Resize_Slite(DSsubviewR,
                   decoded_viewR,
                   DSsubviewR.Length,
                   container.swidth,
                   container.sheight,
                   container.owidth,
                   container.oheight);
            container.setView(temp_, decoded_viewR);
        }
        public void renderpart_bDS(int iter, subseg_container subcontainer, view_container container)
        {
            int temp_ = 3;
            int _iter = iter;
            temp_viewB = subcontainer.getView(temp_, _iter);
            int err = decoding(temp_viewB,
                          DSsubviewB,
                          temp_viewB.Length,
                          container.swidth,
                          container.sheight,
                          container.bpp);
            Resize_Slite(DSsubviewB,
                   decoded_viewB,
                   DSsubviewB.Length,
                   container.swidth,
                   container.sheight,
                   container.owidth,
                   container.oheight);
            container.setView(temp_, decoded_viewB);
        }

        public void onlydecoding_DS(int iter, pre_renderViews container, int idx, int digit)
        {
            int temp_ = 0;
            int _iter = iter;
            temp_viewOnlyDec = cache.GetView(idx, iter, digit);
            int err = decoding(temp_viewOnlyDec,
                          DSsubviewOnlyDec,
                          temp_viewOnlyDec.Length,
                          container.swidth,
                          container.sheight,
                          container.bpp);
            Resize_Slite(DSsubviewOnlyDec,
                   decoded_viewOnlyDec,
                   DSsubviewOnlyDec.Length,
                   container.swidth,
                   container.sheight,
                   container.owidth,
                   container.oheight);
            container.setView(temp_, decoded_viewOnlyDec, iter);
        }
        public void onlydecoding(int iter, pre_renderViews container, int idx, int digit)
        {
            int temp_ = 0;
            int _iter = iter;
            temp_viewOnlyDec = cache.GetView(idx, iter, digit);
            int err = decoding(temp_viewOnlyDec,
                          decoded_viewOnlyDec,
                          temp_viewOnlyDec.Length,
                          container.swidth,
                          container.sheight,
                          container.bpp);
            container.setView(temp_, decoded_viewOnlyDec, iter);
        }
        */
        /*
        public void renderpart_lDS(int iter, subseg_container subcontainer, view_container container, ref Task Resize)
        {
            int temp_ = 0;
            int _iter = iter;
            byte[] temp_view = subcontainer.getView(temp_, _iter);
            int err = decoding(temp_view,
                          DSsubview,
                          temp_view.Length,
                          container.swidth,
                          container.sheight,
                          container.bpp);
            Resize = Task.Run(() =>
            {
                Resize_Slite(DSsubview,
                   decoded_view,
                   DSsubview.Length,
                   container.swidth,
                   container.sheight,
                   container.owidth,
                   container.oheight);
            });
            container.setView(temp_, decoded_view);
        }
        public void renderpart_fDS(int iter, subseg_container subcontainer, view_container container, ref Task Resize)
        {
            int temp_ = 1;
            int _iter = iter;
            byte[] temp_view = subcontainer.getView(temp_, _iter);
            int err = decoding(temp_view,
                          DSsubview,
                          temp_view.Length,
                          container.swidth,
                          container.sheight,
                          container.bpp);
            Resize = Task.Run(() =>
            {
                Resize_Slite(DSsubview,
                decoded_view,
                DSsubview.Length,
                container.swidth,
                container.sheight,
                container.owidth,
                container.oheight);
            });
            
            container.setView(temp_, decoded_view);
        }
        public void renderpart_rDS(int iter, subseg_container subcontainer, view_container container, ref Task Resize)
        {
            int temp_ = 2;
            int _iter = iter;
            byte[] temp_view = subcontainer.getView(temp_, _iter);
            int err = decoding(temp_view,
                          DSsubview,
                          temp_view.Length,
                          container.swidth,
                          container.sheight,
                          container.bpp);
            Resize = Task.Run(() =>
            {
                Resize_Slite(DSsubview,
                   decoded_view,
                   DSsubview.Length,
                   container.swidth,
                   container.sheight,
                   container.owidth,
                   container.oheight);
            });
           
            container.setView(temp_, decoded_view);
        }
        public void renderpart_bDS(int iter, subseg_container subcontainer, view_container container, ref Task Resize)
        {
            int temp_ = 3;
            int _iter = iter;
            byte[] temp_view = subcontainer.getView(temp_, _iter);
            int err = decoding(temp_view,
                          DSsubview,
                          temp_view.Length,
                          container.swidth,
                          container.sheight,
                          container.bpp);
            Resize = Task.Run(() =>
            {
                Resize_Slite(DSsubview,
                   decoded_view,
                   DSsubview.Length,
                   container.swidth,
                   container.sheight,
                   container.owidth,
                   container.oheight);
            });
            container.setView(temp_, decoded_view);
        }

        public void onlydecoding_DS(int iter, pre_renderViews container, int idx, int digit, ref  Task Resize)
        {
            int temp_ = 0;
            int _iter = iter;
            byte[] temp_view = cache.GetView(idx, iter, digit);
            int err = decoding(temp_view,
                          DSsubview,
                          temp_view.Length,
                          container.swidth,
                          container.sheight,
                          container.bpp);
            Resize = Task.Run(() =>
            {
                Resize_Slite(DSsubview,
                   decoded_view,
                   DSsubview.Length,
                   container.swidth,
                   container.sheight,
                   container.owidth,
                   container.oheight);
            });
            container.setView(temp_, decoded_view, iter);
        }
        */


        #endregion
    }
}
