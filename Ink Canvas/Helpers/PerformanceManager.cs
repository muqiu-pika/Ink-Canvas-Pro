using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using Ink_Canvas.Helpers;
using Ink_Canvas.Resources;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 性能管理器 - 整合所有性能优化功能
    /// </summary>
    public class PerformanceManager
    {
        private static readonly object _lockObject = new object();
        private static PerformanceManager _instance;
        public static PerformanceManager Instance => _instance ??= new PerformanceManager();

        // 性能优化器实例
        private readonly PerformanceOptimizer _optimizer = PerformanceOptimizer.Instance;
        private readonly RenderingOptimizer _renderer = RenderingOptimizer.Instance;
        private readonly StrokeRenderingOptimizer _strokeRenderer = new StrokeRenderingOptimizer();

        // 性能监控
        private readonly Stopwatch _performanceStopwatch = new Stopwatch();
        private readonly PerformanceCounter _cpuCounter;
        private readonly PerformanceCounter _memoryCounter;

        // 设置
        private PerformanceSettings _settings;

        public PerformanceManager()
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
            }
            catch
            {
                // 性能计数器不可用，使用默认值
            }
        }

        /// <summary>
        /// 初始化性能管理器
        /// </summary>
        public void Initialize(PerformanceSettings settings)
        {
            _settings = settings;
            ApplySettings();
        }

        /// <summary>
        /// 应用性能设置
        /// </summary>
        private void ApplySettings()
        {
            if (_settings == null) return;

            // 应用节流设置
            _optimizer.CanProcessUpdate(); // 重置节流计时器

            // 应用渲染设置
            _renderer.EnableCaching = _settings.EnableRenderingCache;
            _renderer.EnableRegionRedraw = _settings.EnableRegionRedraw;

            // 应用对象池设置
            if (!_settings.EnableObjectPooling)
            {
                // 清理对象池
                _optimizer.Dispose();
            }

            LogHelper.WriteLogToFile($"Performance settings applied: Profile={_settings.PerformanceProfile}", LogHelper.LogType.Info);
        }

        /// <summary>
        /// 优化笔迹变换操作
        /// </summary>
        public async Task OptimizeStrokeTransform(InkCanvas inkCanvas, StrokeCollection strokes, Matrix transform, bool updateDrawingAttributes = false, Vector scale = new Vector())
        {
            if (_settings == null || !_settings.EnableOptimizedTransform) return;

            try
            {
                if (_settings.EnableAsyncProcessing)
                {
                    await _optimizer.ProcessTransformAsync(() =>
                    {
                        ApplyStrokeTransform(inkCanvas, strokes, transform, updateDrawingAttributes, scale);
                    });
                }
                else
                {
                    ApplyStrokeTransform(inkCanvas, strokes, transform, updateDrawingAttributes, scale);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"OptimizeStrokeTransform error: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 应用笔迹变换
        /// </summary>
        private void ApplyStrokeTransform(InkCanvas inkCanvas, StrokeCollection strokes, Matrix transform, bool updateDrawingAttributes, Vector scale)
        {
            if (_settings.EnableBatchProcessing)
            {
                // 批量处理
                var batchSize = _settings.MaxStrokeTransformBatchSize;
                for (int i = 0; i < strokes.Count; i += batchSize)
                {
                    var batch = new StrokeCollection();
                    for (int j = i; j < Math.Min(i + batchSize, strokes.Count); j++)
                    {
                        batch.Add(strokes[j]);
                    }
                    ProcessStrokeBatch(batch, transform, updateDrawingAttributes, scale);
                }
            }
            else
            {
                // 逐个处理
                foreach (var stroke in strokes)
                {
                    ProcessSingleStroke(stroke, transform, updateDrawingAttributes, scale);
                }
            }
        }

        /// <summary>
        /// 处理笔迹批次
        /// </summary>
        private void ProcessStrokeBatch(StrokeCollection batch, Matrix transform, bool updateDrawingAttributes, Vector scale)
        {
            foreach (var stroke in batch)
            {
                ProcessSingleStroke(stroke, transform, updateDrawingAttributes, scale);
            }
        }

        /// <summary>
        /// 处理单个笔迹
        /// </summary>
        private void ProcessSingleStroke(Stroke stroke, Matrix transform, bool updateDrawingAttributes, Vector scale)
        {
            if (_settings.EnableObjectPooling)
            {
                var matrix = _optimizer.GetMatrix();
                matrix.SetIdentity();
                matrix.M11 = transform.M11;
                matrix.M12 = transform.M12;
                matrix.M21 = transform.M21;
                matrix.M22 = transform.M22;
                matrix.OffsetX = transform.OffsetX;
                matrix.OffsetY = transform.OffsetY;
                stroke.Transform(matrix, false);
                _optimizer.ReturnMatrix(matrix);
            }
            else
            {
                stroke.Transform(transform, false);
            }

                            if (updateDrawingAttributes && scale != new Vector())
            {
                try
                {
                    stroke.DrawingAttributes.Width *= scale.X;
                    stroke.DrawingAttributes.Height *= scale.Y;
                }
                catch { }
            }

            // 标记笔迹为脏状态
            if (_settings.EnableRenderingCache)
            {
                _renderer.MarkStrokeDirty(stroke);
            }
        }

        /// <summary>
        /// 优化笔迹渲染
        /// </summary>
        public async Task OptimizeStrokeRendering(InkCanvas inkCanvas, Stroke stroke)
        {
            if (_settings == null || !_settings.EnableRenderingCache) return;

            try
            {
                if (_settings.EnableAsyncProcessing)
                {
                    await _strokeRenderer.OptimizeStrokeRendering(inkCanvas, stroke);
                }
                else
                {
                    _renderer.CacheStroke(stroke);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"OptimizeStrokeRendering error: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 优化笔迹集合渲染
        /// </summary>
        public async Task OptimizeStrokeCollectionRendering(InkCanvas inkCanvas, StrokeCollection strokes)
        {
            if (_settings == null || !_settings.EnableRenderingCache) return;

            try
            {
                if (_settings.EnableAsyncProcessing)
                {
                    await _strokeRenderer.OptimizeStrokeCollectionRendering(inkCanvas, strokes);
                }
                else
                {
                    _renderer.OptimizeStrokeCollection(inkCanvas, strokes);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"OptimizeStrokeCollectionRendering error: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 智能节流处理
        /// </summary>
        public void SmartThrottle(ManipulationDeltaEventArgs e, Action<ManipulationDeltaEventArgs> processAction)
        {
            if (_settings == null || !_settings.EnableThrottling)
            {
                processAction?.Invoke(e);
                return;
            }

            _optimizer.SmartThrottle(e, processAction);
        }

        /// <summary>
        /// 检查是否可以处理更新
        /// </summary>
        public bool CanProcessUpdate()
        {
            if (_settings == null || !_settings.EnableThrottling) return true;
            return _optimizer.CanProcessUpdate();
        }

        /// <summary>
        /// 获取性能信息
        /// </summary>
        public PerformanceInfo GetPerformanceInfo()
        {
            var info = new PerformanceInfo();

            try
            {
                if (_cpuCounter != null)
                {
                    info.CpuUsage = _cpuCounter.NextValue();
                }

                if (_memoryCounter != null)
                {
                    info.AvailableMemory = _memoryCounter.NextValue();
                }

                info.CacheSize = _renderer.GetCachedStrokeCount();
                info.DirtyRegionsCount = _renderer.GetDirtyRegions().Count;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"GetPerformanceInfo error: {ex}", LogHelper.LogType.Error);
            }

            return info;
        }

        /// <summary>
        /// 开始性能监控
        /// </summary>
        public void BeginPerformanceMonitoring()
        {
            if (_settings != null && _settings.EnablePerformanceMonitoring)
            {
                _performanceStopwatch.Restart();
            }
        }

        /// <summary>
        /// 结束性能监控
        /// </summary>
        public void EndPerformanceMonitoring(string operation, int itemCount = 0)
        {
            if (_settings != null && _settings.EnablePerformanceMonitoring)
            {
                _performanceStopwatch.Stop();
                var elapsedMs = _performanceStopwatch.ElapsedMilliseconds;
                
                if (elapsedMs > 16) // 超过一帧的时间
                {
                    LogHelper.WriteLogToFile($"Performance warning: {operation} took {elapsedMs}ms for {itemCount} items", LogHelper.LogType.Error);
                }
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Cleanup()
        {
            try
            {
                _optimizer.Dispose();
                _renderer.ClearCache();
                _strokeRenderer.Cleanup();

                _cpuCounter?.Dispose();
                _memoryCounter?.Dispose();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"PerformanceManager cleanup error: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 应用性能配置文件
        /// </summary>
        public void ApplyPerformanceProfile(PerformanceProfile profile)
        {
            if (_settings != null)
            {
                _settings.ApplyPerformanceProfile(profile);
                ApplySettings();
            }
        }
    }

    /// <summary>
    /// 性能信息
    /// </summary>
    public class PerformanceInfo
    {
        public float CpuUsage { get; set; }
        public float AvailableMemory { get; set; }
        public int CacheSize { get; set; }
        public int DirtyRegionsCount { get; set; }
        public long ElapsedMilliseconds { get; set; }
    }
} 