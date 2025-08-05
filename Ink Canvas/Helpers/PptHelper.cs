/*
 * @file		PptHelper.cs
 * @brief		PPT 功能助手类
 * @note		提供 PPT 功能管理和错误处理，支持 PowerPoint 和 WPS
 *
 * @envir		VisualStudio 2022 | .NET Framework 4.0 | Windows 10/11
 * @site		https://github.com/InkCanvasPro/Ink-Canvas-Pro
 *
 * @author		Ink Canvas Pro Team
 * @email		support@inkcanvaspro.com
*/

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Office.Interop.PowerPoint;
using Microsoft.Office.Core;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// PPT 软件类型枚举
    /// </summary>
    public enum PptSoftwareType
    {
        PowerPoint,
        WPS,
        Unknown
    }

    /// <summary>
    /// PPT 状态信息
    /// </summary>
    public class PptStatusInfo
    {
        public bool IsConnected { get; set; }
        public bool IsSlideShowActive { get; set; }
        public int CurrentSlideIndex { get; set; }
        public int TotalSlides { get; set; }
        public PptSoftwareType SoftwareType { get; set; }
        public string PresentationTitle { get; set; }
        public bool HasHiddenSlides { get; set; }
        public bool HasAutoPlaySettings { get; set; }
        public DateTime LastUpdateTime { get; set; }
    }

    /// <summary>
    /// PPT 功能助手类
    /// </summary>
    public class PptHelper
    {
        private static PptHelper _instance;
        private static readonly object _lock = new object();

        public static PptHelper Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new PptHelper();
                        }
                    }
                }
                return _instance;
            }
        }

        private Application _pptApplication;
        private Presentation _presentation;
        private SlideShowWindow _slideShowWindow;
        private bool _isConnected = false;
        private bool _isEventBound = false;
        private Process _wpsProcess = null;
        private bool _hasWpsProcessId = false;
        private PptSoftwareType _softwareType = PptSoftwareType.Unknown;
        private string _presentationTitle = "";
        private DateTime _lastUpdateTime = DateTime.Now;

        // 状态信息
        public PptStatusInfo StatusInfo { get; private set; } = new PptStatusInfo();

        // 事件
        public event EventHandler<SlideShowEventArgs> SlideShowBegin;
        public event EventHandler<SlideShowEventArgs> SlideShowNextSlide;
        public event EventHandler<SlideShowEventArgs> SlideShowEnd;
        public event EventHandler<PresentationEventArgs> PresentationClose;
        public event EventHandler<PptStatusChangedEventArgs> StatusChanged;

        private PptHelper() { }

        /// <summary>
        /// 检查PowerPoint是否可用
        /// </summary>
        /// <returns></returns>
        public bool IsPowerPointAvailable()
        {
            try
            {
                var testApp = new Application();
                Marshal.ReleaseComObject(testApp);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 检查WPS是否可用
        /// </summary>
        /// <returns></returns>
        public bool IsWPSAvailable()
        {
            try
            {
                Process[] wpsProcesses = Process.GetProcessesByName("wpp");
                return wpsProcesses.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 检测PPT软件类型
        /// </summary>
        /// <returns></returns>
        public PptSoftwareType DetectPptSoftware()
        {
            if (IsWPSAvailable())
            {
                return PptSoftwareType.WPS;
            }
            else if (IsPowerPointAvailable())
            {
                return PptSoftwareType.PowerPoint;
            }
            else
            {
                return PptSoftwareType.Unknown;
            }
        }

        /// <summary>
        /// 连接到PowerPoint应用程序
        /// </summary>
        /// <returns></returns>
        public bool ConnectToPowerPoint()
        {
            try
            {
                // 检测软件类型
                _softwareType = DetectPptSoftware();
                
                if (_softwareType == PptSoftwareType.Unknown)
                {
                    LogHelper.WriteLogToFile("未检测到可用的PPT软件", LogHelper.LogType.Error);
                    return false;
                }

                // 尝试连接到PowerPoint
                _pptApplication = (Application)Marshal.GetActiveObject("PowerPoint.Application");
                
                if (_pptApplication != null && _pptApplication.Presentations.Count > 0)
                {
                    _presentation = _pptApplication.ActivePresentation;
                    _presentationTitle = _presentation.Name;
                    _isConnected = true;
                    
                    // 绑定事件
                    if (!_isEventBound)
                    {
                        BindEvents();
                    }
                    
                    // 更新状态信息
                    UpdateStatusInfo();
                    
                    LogHelper.WriteLogToFile($"成功连接到 {_softwareType}", LogHelper.LogType.Trace);
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"连接PowerPoint失败: {ex.Message}", LogHelper.LogType.Error);
            }
            
            return false;
        }

        /// <summary>
        /// 断开与PowerPoint的连接
        /// </summary>
        public void DisconnectFromPowerPoint()
        {
            try
            {
                if (_isEventBound)
                {
                    UnbindEvents();
                }

                if (_pptApplication != null)
                {
                    Marshal.ReleaseComObject(_pptApplication);
                    _pptApplication = null;
                }

                // 关闭未正确关闭的 WPS 进程
                if (_hasWpsProcessId && _wpsProcess != null && !_wpsProcess.HasExited)
                {
                    try
                    {
                        _wpsProcess.Kill();
                    }
                    catch { }
                    _hasWpsProcessId = false;
                    _wpsProcess = null;
                }

                _isConnected = false;
                _softwareType = PptSoftwareType.Unknown;
                _presentationTitle = "";
                
                // 更新状态信息
                UpdateStatusInfo();
                
                LogHelper.WriteLogToFile("已断开PowerPoint连接", LogHelper.LogType.Trace);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"断开PowerPoint连接失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 绑定PowerPoint事件
        /// </summary>
        private void BindEvents()
        {
            if (_pptApplication != null)
            {
                _pptApplication.SlideShowBegin += OnSlideShowBegin;
                _pptApplication.SlideShowNextSlide += OnSlideShowNextSlide;
                _pptApplication.SlideShowEnd += OnSlideShowEnd;
                _pptApplication.PresentationClose += OnPresentationClose;
                _isEventBound = true;
            }
        }

        /// <summary>
        /// 解绑PowerPoint事件
        /// </summary>
        private void UnbindEvents()
        {
            if (_pptApplication != null)
            {
                _pptApplication.SlideShowBegin -= OnSlideShowBegin;
                _pptApplication.SlideShowNextSlide -= OnSlideShowNextSlide;
                _pptApplication.SlideShowEnd -= OnSlideShowEnd;
                _pptApplication.PresentationClose -= OnPresentationClose;
                _isEventBound = false;
            }
        }

        /// <summary>
        /// 更新状态信息
        /// </summary>
        private void UpdateStatusInfo()
        {
            var oldStatus = StatusInfo;
            
            StatusInfo.IsConnected = _isConnected;
            StatusInfo.SoftwareType = _softwareType;
            StatusInfo.PresentationTitle = _presentationTitle;
            StatusInfo.LastUpdateTime = DateTime.Now;
            
            if (_isConnected && _presentation != null)
            {
                try
                {
                    StatusInfo.TotalSlides = _presentation.Slides.Count;
                    StatusInfo.HasHiddenSlides = HasHiddenSlides();
                    StatusInfo.HasAutoPlaySettings = HasAutoPlaySettings();
                    
                    if (_pptApplication != null && _pptApplication.SlideShowWindows.Count > 0)
                    {
                        StatusInfo.IsSlideShowActive = true;
                        try
                        {
                            StatusInfo.CurrentSlideIndex = _pptApplication.SlideShowWindows[1].View.Slide.SlideIndex;
                        }
                        catch (System.Runtime.InteropServices.COMException ex)
                        {
                            LogHelper.WriteLogToFile($"获取当前幻灯片索引失败: {ex.Message}", LogHelper.LogType.Error);
                            StatusInfo.CurrentSlideIndex = -1;
                        }
                    }
                    else
                    {
                        StatusInfo.IsSlideShowActive = false;
                        StatusInfo.CurrentSlideIndex = -1;
                    }
                }
                catch (System.Runtime.InteropServices.COMException ex)
                {
                    LogHelper.WriteLogToFile($"更新PPT状态信息失败: {ex.Message}", LogHelper.LogType.Error);
                    // 如果COM操作失败，重置状态
                    StatusInfo.TotalSlides = 0;
                    StatusInfo.CurrentSlideIndex = -1;
                    StatusInfo.IsSlideShowActive = false;
                    StatusInfo.HasHiddenSlides = false;
                    StatusInfo.HasAutoPlaySettings = false;
                }
            }
            else
            {
                StatusInfo.TotalSlides = 0;
                StatusInfo.CurrentSlideIndex = -1;
                StatusInfo.IsSlideShowActive = false;
                StatusInfo.HasHiddenSlides = false;
                StatusInfo.HasAutoPlaySettings = false;
            }
            
            // 触发状态变化事件
            if (oldStatus.IsConnected != StatusInfo.IsConnected || 
                oldStatus.IsSlideShowActive != StatusInfo.IsSlideShowActive ||
                oldStatus.CurrentSlideIndex != StatusInfo.CurrentSlideIndex)
            {
                StatusChanged?.Invoke(this, new PptStatusChangedEventArgs(oldStatus, StatusInfo));
            }
        }

        /// <summary>
        /// 检查是否有隐藏幻灯片
        /// </summary>
        /// <returns></returns>
        public bool HasHiddenSlides()
        {
            if (_presentation == null) return false;

            try
            {
                foreach (Slide slide in _presentation.Slides)
                {
                    if (slide.SlideShowTransition.Hidden == MsoTriState.msoTrue)
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"检查隐藏幻灯片失败: {ex.Message}", LogHelper.LogType.Error);
            }

            return false;
        }

        /// <summary>
        /// 取消隐藏所有幻灯片
        /// </summary>
        public void UnhideAllSlides()
        {
            if (_presentation == null) return;

            try
            {
                foreach (Slide slide in _presentation.Slides)
                {
                    if (slide.SlideShowTransition.Hidden == MsoTriState.msoTrue)
                    {
                        slide.SlideShowTransition.Hidden = MsoTriState.msoFalse;
                    }
                }
                
                UpdateStatusInfo();
                LogHelper.WriteLogToFile("已取消隐藏所有幻灯片", LogHelper.LogType.Trace);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"取消隐藏幻灯片失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 检查是否有自动播放设置
        /// </summary>
        /// <returns></returns>
        public bool HasAutoPlaySettings()
        {
            if (_presentation == null) return false;

            try
            {
                foreach (Slide slide in _presentation.Slides)
                {
                    if (slide.SlideShowTransition.AdvanceOnTime == MsoTriState.msoTrue && 
                        slide.SlideShowTransition.AdvanceTime > 0)
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"检查自动播放设置失败: {ex.Message}", LogHelper.LogType.Error);
            }

            return false;
        }

        /// <summary>
        /// 禁用自动播放
        /// </summary>
        public void DisableAutoPlay()
        {
            if (_presentation == null) return;

            try
            {
                _presentation.SlideShowSettings.AdvanceMode = PpSlideShowAdvanceMode.ppSlideShowManualAdvance;
                
                UpdateStatusInfo();
                LogHelper.WriteLogToFile("已禁用自动播放", LogHelper.LogType.Trace);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"禁用自动播放失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 获取当前幻灯片索引
        /// </summary>
        /// <returns></returns>
        public int GetCurrentSlideIndex()
        {
            try
            {
                if (_pptApplication != null && _pptApplication.SlideShowWindows.Count > 0)
                {
                    try
                    {
                        return _pptApplication.SlideShowWindows[1].View.Slide.SlideIndex;
                    }
                    catch (System.Runtime.InteropServices.COMException ex)
                    {
                        LogHelper.WriteLogToFile($"获取当前幻灯片索引失败: {ex.Message}", LogHelper.LogType.Error);
                        return -1;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"获取当前幻灯片索引失败: {ex.Message}", LogHelper.LogType.Error);
            }

            return -1;
        }

        /// <summary>
        /// 获取总幻灯片数
        /// </summary>
        /// <returns></returns>
        public int GetTotalSlides()
        {
            try
            {
                if (_presentation != null)
                {
                    return _presentation.Slides.Count;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"获取总幻灯片数失败: {ex.Message}", LogHelper.LogType.Error);
            }

            return -1;
        }

        /// <summary>
        /// 跳转到指定幻灯片
        /// </summary>
        /// <param name="slideIndex"></param>
        public void GoToSlide(int slideIndex)
        {
            try
            {
                if (_pptApplication != null)
                {
                    try
                    {
                        if (_pptApplication.SlideShowWindows.Count > 0)
                        {
                            _pptApplication.SlideShowWindows[1].View.GotoSlide(slideIndex);
                            UpdateStatusInfo();
                        }
                    }
                    catch (System.Runtime.InteropServices.COMException ex)
                    {
                        LogHelper.WriteLogToFile($"跳转到幻灯片失败: {ex.Message}", LogHelper.LogType.Error);
                    }
                }
                else if (_presentation != null)
                {
                    try
                    {
                        _presentation.Windows[1].View.GotoSlide(slideIndex);
                        UpdateStatusInfo();
                    }
                    catch (System.Runtime.InteropServices.COMException ex)
                    {
                        LogHelper.WriteLogToFile($"跳转到幻灯片失败: {ex.Message}", LogHelper.LogType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"跳转到幻灯片失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 下一张幻灯片
        /// </summary>
        public void NextSlide()
        {
            try
            {
                if (_pptApplication != null)
                {
                    try
                    {
                        if (_pptApplication.SlideShowWindows.Count > 0)
                        {
                            _pptApplication.SlideShowWindows[1].View.Next();
                            UpdateStatusInfo();
                        }
                    }
                    catch (System.Runtime.InteropServices.COMException ex)
                    {
                        LogHelper.WriteLogToFile($"下一张幻灯片失败: {ex.Message}", LogHelper.LogType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"下一张幻灯片失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 上一张幻灯片
        /// </summary>
        public void PreviousSlide()
        {
            try
            {
                if (_pptApplication != null)
                {
                    try
                    {
                        if (_pptApplication.SlideShowWindows.Count > 0)
                        {
                            _pptApplication.SlideShowWindows[1].View.Previous();
                            UpdateStatusInfo();
                        }
                    }
                    catch (System.Runtime.InteropServices.COMException ex)
                    {
                        LogHelper.WriteLogToFile($"上一张幻灯片失败: {ex.Message}", LogHelper.LogType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"上一张幻灯片失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 结束幻灯片放映
        /// </summary>
        public void EndSlideShow()
        {
            try
            {
                if (_pptApplication != null)
                {
                    try
                    {
                        if (_pptApplication.SlideShowWindows.Count > 0)
                        {
                            _pptApplication.SlideShowWindows[1].View.Exit();
                            UpdateStatusInfo();
                        }
                    }
                    catch (System.Runtime.InteropServices.COMException ex)
                    {
                        LogHelper.WriteLogToFile($"结束幻灯片放映失败: {ex.Message}", LogHelper.LogType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"结束幻灯片放映失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 检查是否正在放映
        /// </summary>
        /// <returns></returns>
        public bool IsSlideShowActive()
        {
            try
            {
                if (_pptApplication != null)
                {
                    try
                    {
                        return _pptApplication.SlideShowWindows.Count > 0;
                    }
                    catch (System.Runtime.InteropServices.COMException ex)
                    {
                        LogHelper.WriteLogToFile($"检查幻灯片放映状态失败: {ex.Message}", LogHelper.LogType.Error);
                        return false;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"检查幻灯片放映状态失败: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }

        /// <summary>
        /// 获取演示文稿标题
        /// </summary>
        /// <returns></returns>
        public string GetPresentationTitle()
        {
            try
            {
                if (_presentation != null)
                {
                    return _presentation.Name;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"获取演示文稿标题失败: {ex.Message}", LogHelper.LogType.Error);
            }
            
            return "";
        }

        /// <summary>
        /// 获取软件类型
        /// </summary>
        /// <returns></returns>
        public PptSoftwareType GetSoftwareType()
        {
            return _softwareType;
        }

        /// <summary>
        /// 获取PowerPoint应用程序对象
        /// </summary>
        /// <returns></returns>
        public Application GetApplication()
        {
            return _pptApplication;
        }

        /// <summary>
        /// 刷新状态信息
        /// </summary>
        public void RefreshStatus()
        {
            UpdateStatusInfo();
        }

        // 事件处理方法
        private void OnSlideShowBegin(SlideShowWindow Wn)
        {
            _slideShowWindow = Wn;
            UpdateStatusInfo();
            SlideShowBegin?.Invoke(this, new SlideShowEventArgs(Wn));
        }

        private void OnSlideShowNextSlide(SlideShowWindow Wn)
        {
            UpdateStatusInfo();
            SlideShowNextSlide?.Invoke(this, new SlideShowEventArgs(Wn));
        }

        private void OnSlideShowEnd(Presentation Pres)
        {
            UpdateStatusInfo();
            SlideShowEnd?.Invoke(this, new SlideShowEventArgs(null, Pres));
        }

        private void OnPresentationClose(Presentation Pres)
        {
            UpdateStatusInfo();
            PresentationClose?.Invoke(this, new PresentationEventArgs(Pres));
        }
    }

    /// <summary>
    /// 幻灯片放映事件参数
    /// </summary>
    public class SlideShowEventArgs : EventArgs
    {
        public SlideShowWindow SlideShowWindow { get; }
        public Presentation Presentation { get; }

        public SlideShowEventArgs(SlideShowWindow slideShowWindow, Presentation presentation = null)
        {
            SlideShowWindow = slideShowWindow;
            Presentation = presentation ?? slideShowWindow?.Presentation;
        }
    }

    /// <summary>
    /// 演示文稿事件参数
    /// </summary>
    public class PresentationEventArgs : EventArgs
    {
        public Presentation Presentation { get; }

        public PresentationEventArgs(Presentation presentation)
        {
            Presentation = presentation;
        }
    }

    /// <summary>
    /// PPT状态变化事件参数
    /// </summary>
    public class PptStatusChangedEventArgs : EventArgs
    {
        public PptStatusInfo OldStatus { get; }
        public PptStatusInfo NewStatus { get; }

        public PptStatusChangedEventArgs(PptStatusInfo oldStatus, PptStatusInfo newStatus)
        {
            OldStatus = oldStatus;
            NewStatus = newStatus;
        }
    }
} 