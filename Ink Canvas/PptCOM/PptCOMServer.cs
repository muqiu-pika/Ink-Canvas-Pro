/*
 * @file		PptCOMServer.cs
 * @brief		Ink Canvas Pro PPT 联动插件
 * @note		PPT 联动插件 相关模块，参考 Inkeys-main 项目设计
 *
 * @envir		VisualStudio 2022 | .NET Framework 4.0 | Windows 10/11
 * @site		https://github.com/InkCanvasPro/Ink-Canvas-Pro
 *
 * @author		Ink Canvas Pro Team
 * @email		support@inkcanvaspro.com
*/

using System;
using System.Threading;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Windows.Forms;
using Microsoft.Office.Core;
using Microsoft.Office.Interop.PowerPoint;

namespace Ink_Canvas.PptCOM
{
    [ComVisible(true)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("65F6E9C1-63EC-4003-B89F-8F425A3C2FEA")]
    public interface IPptCOMServer
    {
        unsafe bool Initialization(int* TotalPage, int* CurrentPage, bool autoCloseWPSTarget);
        string CheckCOM();
        unsafe int IsPptOpen();
        string slideNameIndex();
        unsafe void NextSlideShow(int check);
        unsafe void PreviousSlideShow();
        IntPtr GetPptHwnd();
        void EndSlideShow();
        bool IsSlideShowActive();
        int GetCurrentSlideIndex();
        int GetTotalSlides();
    }

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    [Guid("C44270BE-9A52-400F-AD7C-ED42050A77D8")]
    public class PptCOMServer : IPptCOMServer
    {
        private Microsoft.Office.Interop.PowerPoint.Application pptApp;
        private Microsoft.Office.Interop.PowerPoint.Presentation pptActDoc;
        private Microsoft.Office.Interop.PowerPoint.SlideShowWindow pptActWindow;

        private unsafe int* pptTotalPage;
        private unsafe int* pptCurrentPage;

        private int polling = 0; // 结束界面轮询（0正常页 1/2末页或结束放映页）（2设定为运行一次不被检查的翻页）
        private DateTime updateTime; // 更新时间点
        private bool bindingEvents;

        private bool autoCloseWPS = false;
        private bool hasWpsProcessID;
        private Process wpsProcess;

        // 初始化函数
        public unsafe bool Initialization(int* TotalPage, int* CurrentPage, bool autoCloseWPSTarget)
        {
            try
            {
                pptTotalPage = TotalPage;
                pptCurrentPage = CurrentPage;
                autoCloseWPS = autoCloseWPSTarget;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public string CheckCOM()
        {
            string ret = "20250101a";
            try
            {
                Microsoft.Office.Interop.PowerPoint.Application pptTest = new Microsoft.Office.Interop.PowerPoint.Application();
                Marshal.ReleaseComObject(pptTest);
            }
            catch (Exception ex)
            {
                ret += "\n" + ex.Message;
            }
            return ret;
        }

        // 外部引用函数
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        // 事件查询函数
        private unsafe void SlideShowChange(Microsoft.Office.Interop.PowerPoint.SlideShowWindow Wn)
        {
            updateTime = DateTime.Now;
            try
            {
                *pptCurrentPage = pptActWindow.View.Slide.SlideIndex;
                if (pptActWindow.View.Slide.SlideIndex >= pptActDoc.Slides.Count) polling = 1;
                else polling = 0;
            }
            catch
            {
                *pptCurrentPage = -1;
                polling = 1;
            }
        }

        private unsafe void SlideShowBegin(Microsoft.Office.Interop.PowerPoint.SlideShowWindow Wn)
        {
            updateTime = DateTime.Now;
            pptActWindow = Wn;
            try
            {
                if (pptActWindow.View.Slide.SlideIndex >= pptActDoc.Slides.Count) polling = 1;
                else polling = 0;
            }
            catch
            {
                polling = 1;
            }
            *pptTotalPage = pptActDoc.Slides.Count;
            *pptCurrentPage = pptActWindow.View.Slide.SlideIndex;
        }

        private unsafe void SlideShowShowEnd(Microsoft.Office.Interop.PowerPoint.Presentation Wn)
        {
            updateTime = DateTime.Now;
            *pptCurrentPage = -1;
            *pptTotalPage = -1;
        }

        private void PresentationBeforeClose(Microsoft.Office.Interop.PowerPoint.Presentation Wn, ref bool cancel)
        {
            if (bindingEvents && Wn == pptActDoc)
            {
                pptApp.SlideShowNextSlide -= new Microsoft.Office.Interop.PowerPoint.EApplication_SlideShowNextSlideEventHandler(SlideShowChange);
                pptApp.SlideShowBegin -= new Microsoft.Office.Interop.PowerPoint.EApplication_SlideShowBeginEventHandler(SlideShowBegin);
                pptApp.SlideShowEnd -= new Microsoft.Office.Interop.PowerPoint.EApplication_SlideShowEndEventHandler(SlideShowShowEnd);
                pptApp.PresentationBeforeClose -= new Microsoft.Office.Interop.PowerPoint.EApplication_PresentationBeforeCloseEventHandler(PresentationBeforeClose);
                bindingEvents = false;
            }
            
            // 对于延迟未关闭的 WPS，先记录进程 ID
            if (autoCloseWPS && Wn.Application.Path.Contains("Kingsoft\\WPS Office\\") && Wn.Application.Presentations.Count <= 1)
            {
                try
                {
                    uint processId;
                    GetWindowThreadProcessId((IntPtr)Wn.Application.HWND, out processId);
                    wpsProcess = Process.GetProcessById((int)processId);
                    hasWpsProcessID = true;
                }
                catch { }
            }
            cancel = false;
        }

        // 判断是否有 Ppt 文件被打开（并注册事件）
        public unsafe int IsPptOpen()
        {
            int ret = 0;
            bindingEvents = false;
            hasWpsProcessID = false;

            // 通用尝试，获取 Active 的 Application 并检测是否正确
            try
            {
                pptApp = (Microsoft.Office.Interop.PowerPoint.Application)Marshal.GetActiveObject("PowerPoint.Application");
                ret = pptApp.Presentations.Count;
            }
            catch { }

            // 锁定 Application 并执行后续操作
            if (ret > 0)
            {
                try
                {
                    pptActDoc = pptApp.ActivePresentation;
                    updateTime = DateTime.Now;

                    int tempTotalPage;
                    try
                    {
                        pptActWindow = pptActDoc.SlideShowWindow;
                        *pptTotalPage = tempTotalPage = pptActDoc.Slides.Count;
                    }
                    catch
                    {
                        *pptTotalPage = tempTotalPage = -1;
                    }

                    if (tempTotalPage == -1)
                    {
                        *pptCurrentPage = -1;
                        polling = 0;
                    }
                    else
                    {
                        try
                        {
                            *pptCurrentPage = pptActWindow.View.Slide.SlideIndex;
                            if (pptActWindow.View.Slide.SlideIndex >= pptActDoc.Slides.Count) polling = 1;
                            else polling = 0;
                        }
                        catch
                        {
                            *pptCurrentPage = -1;
                            polling = 1;
                        }
                    }

                    // 绑定事件
                    bindingEvents = true;
                    pptApp.SlideShowNextSlide += new Microsoft.Office.Interop.PowerPoint.EApplication_SlideShowNextSlideEventHandler(SlideShowChange);
                    pptApp.SlideShowBegin += new Microsoft.Office.Interop.PowerPoint.EApplication_SlideShowBeginEventHandler(SlideShowBegin);
                    pptApp.SlideShowEnd += new Microsoft.Office.Interop.PowerPoint.EApplication_SlideShowEndEventHandler(SlideShowShowEnd);
                    pptApp.PresentationBeforeClose += new Microsoft.Office.Interop.PowerPoint.EApplication_PresentationBeforeCloseEventHandler(PresentationBeforeClose);

                    try
                    {
                        while (true)
                        {
                            if (pptActDoc != pptApp.ActivePresentation) break;
                            if (polling != 0)
                            {
                                try
                                {
                                    *pptCurrentPage = pptActWindow.View.Slide.SlideIndex;
                                    polling = 2;
                                }
                                catch
                                {
                                    *pptCurrentPage = -1;
                                }
                            }

                            // 计时轮询（超过3秒不刷新就轮询一次）
                            if ((DateTime.Now - updateTime).TotalMilliseconds > 3000)
                            {
                                try
                                {
                                    if (pptActDoc.SlideShowWindow != null) *pptTotalPage = tempTotalPage = pptActDoc.Slides.Count;
                                    else *pptTotalPage = tempTotalPage = -1;
                                }
                                catch
                                {
                                    *pptTotalPage = tempTotalPage = -1;
                                }

                                if (tempTotalPage == -1)
                                {
                                    *pptCurrentPage = -1;
                                    polling = 0;
                                }
                                else
                                {
                                    try
                                    {
                                        *pptCurrentPage = pptActWindow.View.Slide.SlideIndex;
                                        if (pptActWindow.View.Slide.SlideIndex >= pptActDoc.Slides.Count) polling = 1;
                                        else polling = 0;
                                    }
                                    catch
                                    {
                                        *pptCurrentPage = -1;
                                        polling = 1;
                                    }
                                }
                                updateTime = DateTime.Now;
                            }
                            Thread.Sleep(500);
                        }
                    }
                    catch { }

                    // 解绑事件
                    if (bindingEvents)
                    {
                        pptApp.SlideShowNextSlide -= new Microsoft.Office.Interop.PowerPoint.EApplication_SlideShowNextSlideEventHandler(SlideShowChange);
                        pptApp.SlideShowBegin -= new Microsoft.Office.Interop.PowerPoint.EApplication_SlideShowBeginEventHandler(SlideShowBegin);
                        pptApp.SlideShowEnd -= new Microsoft.Office.Interop.PowerPoint.EApplication_SlideShowEndEventHandler(SlideShowShowEnd);
                        pptApp.PresentationBeforeClose -= new Microsoft.Office.Interop.PowerPoint.EApplication_PresentationBeforeCloseEventHandler(PresentationBeforeClose);
                        bindingEvents = false;
                    }
                    
                    // 关闭未正确关闭的 WPP 进程
                    if (hasWpsProcessID == true && wpsProcess != null && !wpsProcess.HasExited)
                    {
                        try
                        {
                            wpsProcess.Kill();
                        }
                        catch { }
                        hasWpsProcessID = false;
                    }
                }
                catch { }
            }

            *pptCurrentPage = -1; *pptTotalPage = -1;
            return ret;
        }

        // 信息获取函数
        public string slideNameIndex()
        {
            string slidesName = "";
            try
            {
                slidesName += pptActDoc.FullName + "\n";
                slidesName += pptApp.Caption;
            }
            catch { }
            return slidesName;
        }

        public IntPtr GetPptHwnd()
        {
            IntPtr hWnd = IntPtr.Zero;
            try
            {
                pptApp = (Microsoft.Office.Interop.PowerPoint.Application)Marshal.GetActiveObject("PowerPoint.Application");
                pptActDoc = pptApp.ActivePresentation;
                pptActWindow = pptActDoc.SlideShowWindow;
                hWnd = new IntPtr(pptActWindow.HWND);
            }
            catch { }
            return hWnd;
        }

        // 新增：检查幻灯片放映是否激活
        public bool IsSlideShowActive()
        {
            try
            {
                return pptApp.SlideShowWindows.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        // 新增：获取当前幻灯片索引
        public int GetCurrentSlideIndex()
        {
            try
            {
                if (pptApp.SlideShowWindows.Count > 0)
                {
                    return pptApp.SlideShowWindows[1].View.Slide.SlideIndex;
                }
                return -1;
            }
            catch
            {
                return -1;
            }
        }

        // 新增：获取总幻灯片数
        public int GetTotalSlides()
        {
            try
            {
                return pptActDoc.Slides.Count;
            }
            catch
            {
                return -1;
            }
        }

        // 操控函数
        public unsafe void NextSlideShow(int check)
        {
            try
            {
                int temp_SlideIndex = pptActWindow.View.Slide.SlideIndex;
                if (temp_SlideIndex != check && check != -1) return;

                // 下一页
                if (polling != 0)
                {
                    if (polling == 2)
                    {
                        pptActWindow.View.Next();
                    }
                    else if (polling == 1)
                    {
                        int currentPageTemp = -1;
                        try
                        {
                            currentPageTemp = pptActWindow.View.Slide.SlideIndex;
                        }
                        catch
                        {
                            currentPageTemp = -1;
                        }
                        if (currentPageTemp != -1)
                        {
                            pptActWindow.View.Next();
                        }
                    }
                    polling = 1;
                }
                else
                {
                    pptActWindow.View.Next();
                }
            }
            catch { }
        }

        public unsafe void PreviousSlideShow()
        {
            try
            {
                pptActWindow.View.Previous();
            }
            catch { }
        }

        public void EndSlideShow()
        {
            try
            {
                pptActWindow.View.Exit();
            }
            catch { }
        }
    }
} 