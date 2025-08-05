/*
 * @file		PptUIHelper.cs
 * @brief		PPT UI 控件助手类
 * @note		提供 PPT 相关的 UI 控件和界面管理功能
 *
 * @envir		VisualStudio 2022 | .NET Framework 4.0 | Windows 10/11
 * @site		https://github.com/InkCanvasPro/Ink-Canvas-Pro
 *
 * @author		Ink Canvas Pro Team
 * @email		support@inkcanvaspro.com
*/

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// PPT UI 控件状态枚举
    /// </summary>
    public enum PptUIState
    {
        Hidden,
        Minimized,
        Normal,
        Expanded
    }

    /// <summary>
    /// PPT UI 控件位置枚举
    /// </summary>
    public enum PptUIPosition
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        Center
    }

    /// <summary>
    /// PPT UI 控件配置
    /// </summary>
    public class PptUIConfig
    {
        public bool ShowBottomBoth { get; set; } = true;
        public bool ShowMiddleBoth { get; set; } = true;
        public bool ShowBottomMiddle { get; set; } = true;
        public float BottomBothWidth { get; set; } = 200f;
        public float BottomBothHeight { get; set; } = 60f;
        public float MiddleBothWidth { get; set; } = 200f;
        public float MiddleBothHeight { get; set; } = 60f;
        public float BottomMiddleWidth { get; set; } = 200f;
        public float BottomMiddleHeight { get; set; } = 60f;
        public float UIScale { get; set; } = 1.0f;
        public bool MemoryPosition { get; set; } = true;
        public PptUIPosition DefaultPosition { get; set; } = PptUIPosition.BottomRight;
    }

    /// <summary>
    /// PPT UI 控件助手类
    /// </summary>
    public class PptUIHelper : INotifyPropertyChanged
    {
        private static PptUIHelper _instance;
        private static readonly object _lock = new object();

        public static PptUIHelper Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new PptUIHelper();
                        }
                    }
                }
                return _instance;
            }
        }

        private PptUIState _currentState = PptUIState.Hidden;
        private PptUIConfig _config = new PptUIConfig();
        private System.Windows.Controls.Canvas _mainCanvas;
        private Window _parentWindow;
        private DispatcherTimer _updateTimer;

        // UI 控件
        private Button _nextButton;
        private Button _previousButton;
        private Button _endButton;
        private TextBlock _slideInfoText;
        private ProgressBar _slideProgressBar;
        private Button _expandButton;
        private Button _minimizeButton;

        // 事件
        public event EventHandler<PptUIStateChangedEventArgs> StateChanged;
        public event EventHandler<PptUIButtonClickedEventArgs> ButtonClicked;

        private PptUIHelper()
        {
            InitializeTimer();
        }

        /// <summary>
        /// 初始化定时器
        /// </summary>
        private void InitializeTimer()
        {
            _updateTimer = new DispatcherTimer();
            _updateTimer.Interval = TimeSpan.FromMilliseconds(500);
            _updateTimer.Tick += OnUpdateTimerTick;
        }

        /// <summary>
        /// 初始化 UI 控件
        /// </summary>
        /// <param name="parentWindow"></param>
        public void InitializeUI(Window parentWindow)
        {
            _parentWindow = parentWindow;
            CreateMainCanvas();
            CreateUIControls();
            SetupEventHandlers();
            
            LogHelper.WriteLogToFile("PPT UI 控件初始化完成", LogHelper.LogType.Trace);
        }

        /// <summary>
        /// 创建主画布
        /// </summary>
        private void CreateMainCanvas()
        {
            _mainCanvas = new System.Windows.Controls.Canvas();
            _mainCanvas.Width = _parentWindow.Width;
            _mainCanvas.Height = _parentWindow.Height;
            _mainCanvas.Background = Brushes.Transparent;
            _mainCanvas.IsHitTestVisible = true;
        }

        /// <summary>
        /// 创建 UI 控件
        /// </summary>
        private void CreateUIControls()
        {
            // 下一张按钮
            _nextButton = CreateButton("下一张", Brushes.LightBlue);
            _nextButton.Click += (s, e) => OnButtonClicked("Next");

            // 上一张按钮
            _previousButton = CreateButton("上一张", Brushes.LightGreen);
            _previousButton.Click += (s, e) => OnButtonClicked("Previous");

            // 结束放映按钮
            _endButton = CreateButton("结束", Brushes.LightCoral);
            _endButton.Click += (s, e) => OnButtonClicked("End");

            // 幻灯片信息文本
            _slideInfoText = new TextBlock
            {
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)),
                Padding = new Thickness(5),
                FontSize = 12,
                Text = "未连接"
            };

            // 幻灯片进度条
            _slideProgressBar = new ProgressBar
            {
                Width = 150,
                Height = 8,
                Background = new SolidColorBrush(Color.FromArgb(128, 128, 128, 128)),
                Foreground = Brushes.LightBlue
            };

            // 展开按钮
            _expandButton = CreateButton("展开", Brushes.LightGray);
            _expandButton.Click += (s, e) => SetState(PptUIState.Expanded);

            // 最小化按钮
            _minimizeButton = CreateButton("最小化", Brushes.LightGray);
            _minimizeButton.Click += (s, e) => SetState(PptUIState.Minimized);

            // 添加到画布
            _mainCanvas.Children.Add(_nextButton);
            _mainCanvas.Children.Add(_previousButton);
            _mainCanvas.Children.Add(_endButton);
            _mainCanvas.Children.Add(_slideInfoText);
            _mainCanvas.Children.Add(_slideProgressBar);
            _mainCanvas.Children.Add(_expandButton);
            _mainCanvas.Children.Add(_minimizeButton);
        }

        /// <summary>
        /// 创建按钮
        /// </summary>
        /// <param name="text"></param>
        /// <param name="background"></param>
        /// <returns></returns>
        private Button CreateButton(string text, Brush background)
        {
            return new Button
            {
                Content = text,
                Background = background,
                Foreground = Brushes.Black,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 4, 8, 4),
                FontSize = 12,
                MinWidth = 60,
                MinHeight = 30
            };
        }

        /// <summary>
        /// 设置事件处理器
        /// </summary>
        private void SetupEventHandlers()
        {
            // 监听 PPT 状态变化
            PptHelper.Instance.StatusChanged += OnPptStatusChanged;
        }

        /// <summary>
        /// 设置 UI 状态
        /// </summary>
        /// <param name="state"></param>
        public void SetState(PptUIState state)
        {
            if (_currentState == state) return;

            var oldState = _currentState;
            _currentState = state;

            switch (state)
            {
                case PptUIState.Hidden:
                    HideAllControls();
                    break;
                case PptUIState.Minimized:
                    ShowMinimizedControls();
                    break;
                case PptUIState.Normal:
                    ShowNormalControls();
                    break;
                case PptUIState.Expanded:
                    ShowExpandedControls();
                    break;
            }

            StateChanged?.Invoke(this, new PptUIStateChangedEventArgs(oldState, state));
            OnPropertyChanged(nameof(CurrentState));
        }

        /// <summary>
        /// 隐藏所有控件
        /// </summary>
        private void HideAllControls()
        {
            _nextButton.Visibility = Visibility.Collapsed;
            _previousButton.Visibility = Visibility.Collapsed;
            _endButton.Visibility = Visibility.Collapsed;
            _slideInfoText.Visibility = Visibility.Collapsed;
            _slideProgressBar.Visibility = Visibility.Collapsed;
            _expandButton.Visibility = Visibility.Collapsed;
            _minimizeButton.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// 显示最小化控件
        /// </summary>
        private void ShowMinimizedControls()
        {
            HideAllControls();
            _expandButton.Visibility = Visibility.Visible;
            PositionControl(_expandButton, PptUIPosition.BottomRight);
        }

        /// <summary>
        /// 显示正常控件
        /// </summary>
        private void ShowNormalControls()
        {
            HideAllControls();
            _nextButton.Visibility = Visibility.Visible;
            _previousButton.Visibility = Visibility.Visible;
            _endButton.Visibility = Visibility.Visible;
            _slideInfoText.Visibility = Visibility.Visible;
            _slideProgressBar.Visibility = Visibility.Visible;
            _minimizeButton.Visibility = Visibility.Visible;

            PositionNormalControls();
        }

        /// <summary>
        /// 显示展开控件
        /// </summary>
        private void ShowExpandedControls()
        {
            ShowNormalControls();
            // 可以添加更多展开时的控件
        }

        /// <summary>
        /// 定位正常控件
        /// </summary>
        private void PositionNormalControls()
        {
            double margin = 10;
            double buttonWidth = 60;
            double buttonHeight = 30;
            double infoHeight = 40;

            // 底部右侧布局
            double right = _mainCanvas.Width - margin;
            double bottom = _mainCanvas.Height - margin;

            // 按钮行
            System.Windows.Controls.Canvas.SetRight(_endButton, margin);
            System.Windows.Controls.Canvas.SetBottom(_endButton, bottom - buttonHeight - margin);

            System.Windows.Controls.Canvas.SetRight(_nextButton, margin + buttonWidth + margin);
            System.Windows.Controls.Canvas.SetBottom(_nextButton, bottom - buttonHeight - margin);

            System.Windows.Controls.Canvas.SetRight(_previousButton, margin + (buttonWidth + margin) * 2);
            System.Windows.Controls.Canvas.SetBottom(_previousButton, bottom - buttonHeight - margin);

            // 信息行
            System.Windows.Controls.Canvas.SetRight(_slideInfoText, margin);
            System.Windows.Controls.Canvas.SetBottom(_slideInfoText, bottom - buttonHeight - margin - infoHeight - margin);

            System.Windows.Controls.Canvas.SetRight(_slideProgressBar, margin);
            System.Windows.Controls.Canvas.SetBottom(_slideProgressBar, bottom - buttonHeight - margin - infoHeight - margin - 8 - margin);

            // 最小化按钮
            System.Windows.Controls.Canvas.SetRight(_minimizeButton, margin);
            System.Windows.Controls.Canvas.SetBottom(_minimizeButton, bottom - buttonHeight - margin - infoHeight - margin - 8 - margin - 30 - margin);
        }

        /// <summary>
        /// 定位控件
        /// </summary>
        /// <param name="control"></param>
        /// <param name="position"></param>
        private void PositionControl(FrameworkElement control, PptUIPosition position)
        {
            double margin = 10;

            switch (position)
            {
                case PptUIPosition.TopLeft:
                    System.Windows.Controls.Canvas.SetLeft(control, margin);
                    System.Windows.Controls.Canvas.SetTop(control, margin);
                    break;
                case PptUIPosition.TopRight:
                    System.Windows.Controls.Canvas.SetRight(control, margin);
                    System.Windows.Controls.Canvas.SetTop(control, margin);
                    break;
                case PptUIPosition.BottomLeft:
                    System.Windows.Controls.Canvas.SetLeft(control, margin);
                    System.Windows.Controls.Canvas.SetBottom(control, margin);
                    break;
                case PptUIPosition.BottomRight:
                    System.Windows.Controls.Canvas.SetRight(control, margin);
                    System.Windows.Controls.Canvas.SetBottom(control, margin);
                    break;
                case PptUIPosition.Center:
                    System.Windows.Controls.Canvas.SetLeft(control, (_mainCanvas.Width - control.Width) / 2);
                    System.Windows.Controls.Canvas.SetTop(control, (_mainCanvas.Height - control.Height) / 2);
                    break;
            }
        }

        /// <summary>
        /// 更新幻灯片信息
        /// </summary>
        public void UpdateSlideInfo()
        {
            var status = PptHelper.Instance.StatusInfo;
            
            if (status.IsConnected)
            {
                string softwareName = status.SoftwareType == PptSoftwareType.WPS ? "WPS" : "PowerPoint";
                string slideInfo = $"第 {status.CurrentSlideIndex} / {status.TotalSlides} 张";
                
                _slideInfoText.Text = $"{softwareName} - {slideInfo}";
                
                if (status.TotalSlides > 0)
                {
                    _slideProgressBar.Value = (double)status.CurrentSlideIndex / status.TotalSlides * 100;
                }
            }
            else
            {
                _slideInfoText.Text = "未连接";
                _slideProgressBar.Value = 0;
            }
        }

        /// <summary>
        /// 开始更新定时器
        /// </summary>
        public void StartUpdateTimer()
        {
            _updateTimer.Start();
        }

        /// <summary>
        /// 停止更新定时器
        /// </summary>
        public void StopUpdateTimer()
        {
            _updateTimer.Stop();
        }

        /// <summary>
        /// 定时器更新事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnUpdateTimerTick(object sender, EventArgs e)
        {
            UpdateSlideInfo();
        }

        /// <summary>
        /// PPT 状态变化事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnPptStatusChanged(object sender, PptStatusChangedEventArgs e)
        {
            // 在 UI 线程中更新
            _parentWindow.Dispatcher.Invoke(() =>
            {
                UpdateSlideInfo();
                
                // 根据连接状态调整 UI
                if (e.NewStatus.IsConnected)
                {
                    if (_currentState == PptUIState.Hidden)
                    {
                        SetState(PptUIState.Normal);
                    }
                }
                else
                {
                    if (_currentState != PptUIState.Hidden)
                    {
                        SetState(PptUIState.Hidden);
                    }
                }
            });
        }

        /// <summary>
        /// 按钮点击事件
        /// </summary>
        /// <param name="action"></param>
        private void OnButtonClicked(string action)
        {
            ButtonClicked?.Invoke(this, new PptUIButtonClickedEventArgs(action));
            
            switch (action)
            {
                case "Next":
                    PptHelper.Instance.NextSlide();
                    break;
                case "Previous":
                    PptHelper.Instance.PreviousSlide();
                    break;
                case "End":
                    PptHelper.Instance.EndSlideShow();
                    break;
            }
        }

        /// <summary>
        /// 获取主画布
        /// </summary>
        /// <returns></returns>
        public System.Windows.Controls.Canvas GetMainCanvas()
        {
            return _mainCanvas;
        }

        /// <summary>
        /// 获取当前状态
        /// </summary>
        public PptUIState CurrentState => _currentState;

        /// <summary>
        /// 获取或设置配置
        /// </summary>
        public PptUIConfig Config
        {
            get => _config;
            set
            {
                _config = value;
                OnPropertyChanged();
            }
        }

        // INotifyPropertyChanged 实现
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// PPT UI 状态变化事件参数
    /// </summary>
    public class PptUIStateChangedEventArgs : EventArgs
    {
        public PptUIState OldState { get; }
        public PptUIState NewState { get; }

        public PptUIStateChangedEventArgs(PptUIState oldState, PptUIState newState)
        {
            OldState = oldState;
            NewState = newState;
        }
    }

    /// <summary>
    /// PPT UI 按钮点击事件参数
    /// </summary>
    public class PptUIButtonClickedEventArgs : EventArgs
    {
        public string Action { get; }

        public PptUIButtonClickedEventArgs(string action)
        {
            Action = action;
        }
    }
} 