/*
 * @file		PptSettingsManager.cs
 * @brief		PPT 设置管理器类
 * @note		管理 PPT 相关的配置和设置
 *
 * @envir		VisualStudio 2022 | .NET Framework 4.0 | Windows 10/11
 * @site		https://github.com/InkCanvasPro/Ink-Canvas-Pro
 *
 * @author		Ink Canvas Pro Team
 * @email		support@inkcanvaspro.com
*/

using System;
using System.IO;
using System.Xml.Serialization;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// PPT 设置类
    /// </summary>
    [Serializable]
    public class PptSettings
    {
        // 基本功能设置
        public bool EnablePptIntegration { get; set; } = true;
        public bool AutoConnectToPpt { get; set; } = true;
        public bool ShowPptUI { get; set; } = true;
        public bool EnableSlideShowDetection { get; set; } = true;

        // UI 控件设置
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

        // 墨迹固定设置
        public bool FixedHandWriting { get; set; } = true;
        public bool ShowLoadingScreen { get; set; } = true;

        // 自动处理设置
        public bool AutoUnhideSlides { get; set; } = true;
        public bool AutoDisableAutoPlay { get; set; } = true;
        public bool AutoKillWpsProcess { get; set; } = false;

        // 软件支持设置
        public bool SupportPowerPoint { get; set; } = true;
        public bool SupportWPS { get; set; } = true;

        // 高级设置
        public int ConnectionTimeout { get; set; } = 5000;
        public int UpdateInterval { get; set; } = 500;
        public bool EnableLogging { get; set; } = true;
        public bool EnableErrorHandling { get; set; } = true;
    }

    /// <summary>
    /// PPT 设置管理器类
    /// </summary>
    public class PptSettingsManager : INotifyPropertyChanged
    {
        private static PptSettingsManager _instance;
        private static readonly object _lock = new object();

        public static PptSettingsManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new PptSettingsManager();
                        }
                    }
                }
                return _instance;
            }
        }

        private PptSettings _settings;
        private string _settingsFilePath;
        private bool _isModified = false;

        public event EventHandler<PptSettingsChangedEventArgs> SettingsChanged;

        private PptSettingsManager()
        {
            _settingsFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "InkCanvasPro",
                "PptSettings.xml"
            );
            
            LoadSettings();
        }

        /// <summary>
        /// 获取设置
        /// </summary>
        public PptSettings Settings
        {
            get => _settings;
            set
            {
                if (_settings != value)
                {
                    var oldSettings = _settings;
                    _settings = value;
                    _isModified = true;
                    
                    SettingsChanged?.Invoke(this, new PptSettingsChangedEventArgs(oldSettings, _settings));
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 加载设置
        /// </summary>
        public void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    using (var stream = new FileStream(_settingsFilePath, FileMode.Open))
                    {
                        var serializer = new XmlSerializer(typeof(PptSettings));
                        _settings = (PptSettings)serializer.Deserialize(stream);
                    }
                    
                    LogHelper.WriteLogToFile("PPT 设置加载成功", LogHelper.LogType.Trace);
                }
                else
                {
                    _settings = new PptSettings();
                    SaveSettings();
                    LogHelper.WriteLogToFile("创建默认 PPT 设置", LogHelper.LogType.Trace);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"加载 PPT 设置失败: {ex.Message}", LogHelper.LogType.Error);
                _settings = new PptSettings();
            }
        }

        /// <summary>
        /// 保存设置
        /// </summary>
        public void SaveSettings()
        {
            try
            {
                // 确保目录存在
                var directory = Path.GetDirectoryName(_settingsFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (var stream = new FileStream(_settingsFilePath, FileMode.Create))
                {
                    var serializer = new XmlSerializer(typeof(PptSettings));
                    serializer.Serialize(stream, _settings);
                }
                
                _isModified = false;
                LogHelper.WriteLogToFile("PPT 设置保存成功", LogHelper.LogType.Trace);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存 PPT 设置失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 重置为默认设置
        /// </summary>
        public void ResetToDefaults()
        {
            var oldSettings = _settings;
            _settings = new PptSettings();
            _isModified = true;
            
            SettingsChanged?.Invoke(this, new PptSettingsChangedEventArgs(oldSettings, _settings));
            OnPropertyChanged(nameof(Settings));
            
            SaveSettings();
            LogHelper.WriteLogToFile("PPT 设置已重置为默认值", LogHelper.LogType.Trace);
        }

        /// <summary>
        /// 检查是否有未保存的更改
        /// </summary>
        /// <returns></returns>
        public bool HasUnsavedChanges()
        {
            return _isModified;
        }

        /// <summary>
        /// 应用设置到 PPT 助手
        /// </summary>
        public void ApplySettingsToPptHelper()
        {
            try
            {
                // 应用 UI 配置
                var uiConfig = new PptUIConfig
                {
                    ShowBottomBoth = _settings.ShowBottomBoth,
                    ShowMiddleBoth = _settings.ShowMiddleBoth,
                    ShowBottomMiddle = _settings.ShowBottomMiddle,
                    BottomBothWidth = _settings.BottomBothWidth,
                    BottomBothHeight = _settings.BottomBothHeight,
                    MiddleBothWidth = _settings.MiddleBothWidth,
                    MiddleBothHeight = _settings.MiddleBothHeight,
                    BottomMiddleWidth = _settings.BottomMiddleWidth,
                    BottomMiddleHeight = _settings.BottomMiddleHeight,
                    UIScale = _settings.UIScale,
                    MemoryPosition = _settings.MemoryPosition,
                    DefaultPosition = _settings.DefaultPosition
                };

                PptUIHelper.Instance.Config = uiConfig;

                LogHelper.WriteLogToFile("PPT 设置已应用到助手", LogHelper.LogType.Trace);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用 PPT 设置失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 更新单个设置项
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="propertyName"></param>
        /// <param name="value"></param>
        public void UpdateSetting<T>(string propertyName, T value)
        {
            var property = typeof(PptSettings).GetProperty(propertyName);
            if (property != null && property.CanWrite)
            {
                var oldValue = property.GetValue(_settings);
                property.SetValue(_settings, value);
                
                if (!oldValue.Equals(value))
                {
                    _isModified = true;
                    OnPropertyChanged(nameof(Settings));
                    
                    LogHelper.WriteLogToFile($"PPT 设置已更新: {propertyName} = {value}", LogHelper.LogType.Trace);
                }
            }
        }

        /// <summary>
        /// 获取设置值
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="propertyName"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public T GetSetting<T>(string propertyName, T defaultValue = default(T))
        {
            var property = typeof(PptSettings).GetProperty(propertyName);
            if (property != null && property.CanRead)
            {
                var value = property.GetValue(_settings);
                if (value is T)
                {
                    return (T)value;
                }
            }
            return defaultValue;
        }

        /// <summary>
        /// 验证设置的有效性
        /// </summary>
        /// <returns></returns>
        public bool ValidateSettings()
        {
            try
            {
                // 检查基本设置
                if (_settings.ConnectionTimeout < 1000 || _settings.ConnectionTimeout > 30000)
                {
                    LogHelper.WriteLogToFile("连接超时设置无效，使用默认值", LogHelper.LogType.Error);
                    _settings.ConnectionTimeout = 5000;
                }

                if (_settings.UpdateInterval < 100 || _settings.UpdateInterval > 5000)
                {
                    LogHelper.WriteLogToFile("更新间隔设置无效，使用默认值", LogHelper.LogType.Error);
                    _settings.UpdateInterval = 500;
                }

                // 检查 UI 尺寸设置
                if (_settings.BottomBothWidth < 50 || _settings.BottomBothWidth > 1000)
                {
                    _settings.BottomBothWidth = 200f;
                }

                if (_settings.BottomBothHeight < 20 || _settings.BottomBothHeight > 500)
                {
                    _settings.BottomBothHeight = 60f;
                }

                if (_settings.UIScale < 0.5f || _settings.UIScale > 3.0f)
                {
                    _settings.UIScale = 1.0f;
                }

                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"验证 PPT 设置失败: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }

        /// <summary>
        /// 导出设置到文件
        /// </summary>
        /// <param name="filePath"></param>
        public void ExportSettings(string filePath)
        {
            try
            {
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    var serializer = new XmlSerializer(typeof(PptSettings));
                    serializer.Serialize(stream, _settings);
                }
                
                LogHelper.WriteLogToFile($"PPT 设置已导出到: {filePath}", LogHelper.LogType.Trace);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"导出 PPT 设置失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 从文件导入设置
        /// </summary>
        /// <param name="filePath"></param>
        public void ImportSettings(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    using (var stream = new FileStream(filePath, FileMode.Open))
                    {
                        var serializer = new XmlSerializer(typeof(PptSettings));
                        var importedSettings = (PptSettings)serializer.Deserialize(stream);
                        
                        var oldSettings = _settings;
                        _settings = importedSettings;
                        _isModified = true;
                        
                        SettingsChanged?.Invoke(this, new PptSettingsChangedEventArgs(oldSettings, _settings));
                        OnPropertyChanged(nameof(Settings));
                        
                        LogHelper.WriteLogToFile($"PPT 设置已从文件导入: {filePath}", LogHelper.LogType.Trace);
                    }
                }
                else
                {
                    LogHelper.WriteLogToFile($"导入文件不存在: {filePath}", LogHelper.LogType.Error);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"导入 PPT 设置失败: {ex.Message}", LogHelper.LogType.Error);
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
    /// PPT 设置变化事件参数
    /// </summary>
    public class PptSettingsChangedEventArgs : EventArgs
    {
        public PptSettings OldSettings { get; }
        public PptSettings NewSettings { get; }

        public PptSettingsChangedEventArgs(PptSettings oldSettings, PptSettings newSettings)
        {
            OldSettings = oldSettings;
            NewSettings = newSettings;
        }
    }
} 