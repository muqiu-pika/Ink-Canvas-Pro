# Ink Canvas Pro PPT 功能使用说明

## 概述

Ink Canvas Pro 的 PPT 功能已经根据 Inkeys-main 项目进行了全面优化，提供了更强大的 PPT 集成功能，支持 PowerPoint 和 WPS 两种软件。

## 主要功能

### 1. PPT 助手类 (PptHelper)

#### 基本使用

```csharp
// 获取 PPT 助手实例
var pptHelper = PptHelper.Instance;

// 检查 PowerPoint 是否可用
bool isPowerPointAvailable = pptHelper.IsPowerPointAvailable();

// 检查 WPS 是否可用
bool isWPSAvailable = pptHelper.IsWPSAvailable();

// 检测 PPT 软件类型
var softwareType = pptHelper.DetectPptSoftware();

// 连接到 PPT
bool connected = pptHelper.ConnectToPowerPoint();

// 获取状态信息
var status = pptHelper.StatusInfo;
```

#### 幻灯片控制

```csharp
// 获取当前幻灯片索引
int currentIndex = pptHelper.GetCurrentSlideIndex();

// 获取总幻灯片数
int totalSlides = pptHelper.GetTotalSlides();

// 跳转到指定幻灯片
pptHelper.GoToSlide(3);

// 下一张幻灯片
pptHelper.NextSlide();

// 上一张幻灯片
pptHelper.PreviousSlide();

// 结束幻灯片放映
pptHelper.EndSlideShow();
```

#### 自动处理功能

```csharp
// 检查是否有隐藏幻灯片
bool hasHidden = pptHelper.HasHiddenSlides();

// 取消隐藏所有幻灯片
pptHelper.UnhideAllSlides();

// 检查是否有自动播放设置
bool hasAutoPlay = pptHelper.HasAutoPlaySettings();

// 禁用自动播放
pptHelper.DisableAutoPlay();
```

#### 事件监听

```csharp
// 监听状态变化
pptHelper.StatusChanged += (sender, e) =>
{
    Console.WriteLine($"PPT 状态变化: {e.NewStatus.IsConnected}");
};

// 监听幻灯片放映开始
pptHelper.SlideShowBegin += (sender, e) =>
{
    Console.WriteLine("幻灯片放映开始");
};

// 监听幻灯片切换
pptHelper.SlideShowNextSlide += (sender, e) =>
{
    Console.WriteLine("幻灯片切换到下一张");
};
```

### 2. PPT UI 控件助手 (PptUIHelper)

#### 初始化 UI

```csharp
// 初始化 UI 控件
var uiHelper = PptUIHelper.Instance;
uiHelper.InitializeUI(mainWindow);

// 开始更新定时器
uiHelper.StartUpdateTimer();

// 获取主画布
var canvas = uiHelper.GetMainCanvas();
```

#### UI 状态控制

```csharp
// 设置 UI 状态
uiHelper.SetState(PptUIState.Normal);    // 正常状态
uiHelper.SetState(PptUIState.Minimized); // 最小化状态
uiHelper.SetState(PptUIState.Expanded);  // 展开状态
uiHelper.SetState(PptUIState.Hidden);    // 隐藏状态

// 监听状态变化
uiHelper.StateChanged += (sender, e) =>
{
    Console.WriteLine($"UI 状态从 {e.OldState} 变为 {e.NewState}");
};

// 监听按钮点击
uiHelper.ButtonClicked += (sender, e) =>
{
    Console.WriteLine($"按钮点击: {e.Action}");
};
```

#### UI 配置

```csharp
// 配置 UI 控件
var config = new PptUIConfig
{
    ShowBottomBoth = true,
    ShowMiddleBoth = true,
    ShowBottomMiddle = true,
    BottomBothWidth = 200f,
    BottomBothHeight = 60f,
    UIScale = 1.0f,
    MemoryPosition = true,
    DefaultPosition = PptUIPosition.BottomRight
};

uiHelper.Config = config;
```

### 3. PPT 设置管理器 (PptSettingsManager)

#### 基本使用

```csharp
// 获取设置管理器实例
var settingsManager = PptSettingsManager.Instance;

// 获取设置
var settings = settingsManager.Settings;

// 更新设置
settingsManager.UpdateSetting("EnablePptIntegration", true);
settingsManager.UpdateSetting("ShowPptUI", true);
settingsManager.UpdateSetting("UIScale", 1.2f);

// 保存设置
settingsManager.SaveSettings();

// 应用设置到 PPT 助手
settingsManager.ApplySettingsToPptHelper();
```

#### 设置验证和重置

```csharp
// 验证设置
bool isValid = settingsManager.ValidateSettings();

// 重置为默认设置
settingsManager.ResetToDefaults();

// 检查是否有未保存的更改
bool hasUnsavedChanges = settingsManager.HasUnsavedChanges();
```

#### 设置导入导出

```csharp
// 导出设置
settingsManager.ExportSettings("C:\\temp\\ppt_settings.xml");

// 导入设置
settingsManager.ImportSettings("C:\\temp\\ppt_settings.xml");
```

## 配置选项

### PPT 设置 (PptSettings)

#### 基本功能设置
- `EnablePptIntegration`: 启用 PPT 集成功能
- `AutoConnectToPpt`: 自动连接到 PPT
- `ShowPptUI`: 显示 PPT UI 控件
- `EnableSlideShowDetection`: 启用幻灯片放映检测

#### UI 控件设置
- `ShowBottomBoth`: 显示底部双侧控件
- `ShowMiddleBoth`: 显示中间双侧控件
- `ShowBottomMiddle`: 显示底部中间控件
- `UIScale`: UI 缩放比例
- `MemoryPosition`: 记忆控件位置
- `DefaultPosition`: 默认控件位置

#### 墨迹固定设置
- `FixedHandWriting`: 墨迹固定在对应页面
- `ShowLoadingScreen`: 显示加载页面

#### 自动处理设置
- `AutoUnhideSlides`: 自动取消隐藏幻灯片
- `AutoDisableAutoPlay`: 自动禁用自动播放
- `AutoKillWpsProcess`: 自动结束 WPS 进程

#### 软件支持设置
- `SupportPowerPoint`: 支持 PowerPoint
- `SupportWPS`: 支持 WPS

#### 高级设置
- `ConnectionTimeout`: 连接超时时间 (毫秒)
- `UpdateInterval`: 更新间隔 (毫秒)
- `EnableLogging`: 启用日志记录
- `EnableErrorHandling`: 启用错误处理

## 使用示例

### 完整的 PPT 集成示例

```csharp
public class PptIntegrationExample
{
    private PptHelper _pptHelper;
    private PptUIHelper _uiHelper;
    private PptSettingsManager _settingsManager;

    public async Task InitializePptIntegration()
    {
        // 初始化各个组件
        _pptHelper = PptHelper.Instance;
        _uiHelper = PptUIHelper.Instance;
        _settingsManager = PptSettingsManager.Instance;

        // 加载和应用设置
        _settingsManager.LoadSettings();
        _settingsManager.ApplySettingsToPptHelper();

        // 设置事件监听
        _pptHelper.StatusChanged += OnPptStatusChanged;
        _uiHelper.StateChanged += OnUIStateChanged;
        _uiHelper.ButtonClicked += OnButtonClicked;

        // 初始化 UI
        _uiHelper.InitializeUI(mainWindow);
        _uiHelper.StartUpdateTimer();

        // 尝试连接 PPT
        if (_settingsManager.Settings.AutoConnectToPpt)
        {
            bool connected = _pptHelper.ConnectToPowerPoint();
            if (connected)
            {
                Console.WriteLine("成功连接到 PPT");
            }
        }
    }

    private void OnPptStatusChanged(object sender, PptStatusChangedEventArgs e)
    {
        Console.WriteLine($"PPT 状态变化: {e.NewStatus.SoftwareType} - {e.NewStatus.PresentationTitle}");
    }

    private void OnUIStateChanged(object sender, PptUIStateChangedEventArgs e)
    {
        Console.WriteLine($"UI 状态变化: {e.OldState} -> {e.NewState}");
    }

    private void OnButtonClicked(object sender, PptUIButtonClickedEventArgs e)
    {
        Console.WriteLine($"按钮点击: {e.Action}");
    }
}
```

## 注意事项

1. **软件兼容性**: 确保系统已安装 PowerPoint 或 WPS
2. **权限要求**: 某些功能可能需要管理员权限
3. **错误处理**: 建议实现适当的错误处理机制
4. **资源管理**: 使用完毕后记得断开连接和释放资源
5. **设置备份**: 建议定期备份 PPT 设置文件

## 故障排除

### 常见问题

1. **无法连接到 PPT**
   - 检查 PowerPoint 或 WPS 是否正在运行
   - 确认软件版本兼容性
   - 尝试以管理员身份运行

2. **UI 控件不显示**
   - 检查 `ShowPptUI` 设置
   - 确认 UI 初始化是否正确
   - 检查窗口层级设置

3. **设置不生效**
   - 确认设置已保存
   - 调用 `ApplySettingsToPptHelper()` 方法
   - 检查设置文件权限

### 日志查看

所有 PPT 相关操作都会记录到日志文件中，可以通过 `LogHelper` 查看详细信息：

```csharp
// 查看日志
LogHelper.WriteLogToFile("PPT 操作日志", LogHelper.LogType.Trace);
```

## 更新日志

### v2.0.0 (基于 Inkeys-main 优化)
- ✅ 支持 PowerPoint 和 WPS 双软件
- ✅ 增强的 UI 控件系统
- ✅ 完整的设置管理系统
- ✅ 改进的错误处理和日志记录
- ✅ 状态监控和事件系统
- ✅ 自动处理功能（隐藏幻灯片、自动播放等）

---

如有问题或建议，请联系 Ink Canvas Pro 团队。 