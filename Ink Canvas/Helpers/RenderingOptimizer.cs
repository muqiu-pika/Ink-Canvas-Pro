using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using Ink_Canvas.Helpers;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 渲染优化助手类
    /// </summary>
    public class RenderingOptimizer
    {
        private static readonly object _lockObject = new object();
        private static RenderingOptimizer _instance;
        public static RenderingOptimizer Instance => _instance ??= new RenderingOptimizer();

        // 渲染缓存
        private readonly Dictionary<Stroke, DrawingVisual> _strokeCache = new Dictionary<Stroke, DrawingVisual>();
        private readonly HashSet<Stroke> _dirtyStrokes = new HashSet<Stroke>();

        // 区域重绘
        private readonly List<Rect> _dirtyRegions = new List<Rect>();
        private readonly object _regionLock = new object();

        // 渲染设置
        private bool _enableCaching = true;
        private bool _enableRegionRedraw = true;
        private const int MaxCacheSize = 1000;

        /// <summary>
        /// 启用或禁用渲染缓存
        /// </summary>
        public bool EnableCaching
        {
            get => _enableCaching;
            set => _enableCaching = value;
        }

        /// <summary>
        /// 启用或禁用区域重绘
        /// </summary>
        public bool EnableRegionRedraw
        {
            get => _enableRegionRedraw;
            set => _enableRegionRedraw = value;
        }

        /// <summary>
        /// 缓存笔迹的渲染结果
        /// </summary>
        public void CacheStroke(Stroke stroke)
        {
            if (!_enableCaching) return;

            lock (_lockObject)
            {
                if (_strokeCache.Count >= MaxCacheSize)
                {
                    // 清理最旧的缓存
                    var oldestStroke = _strokeCache.Keys.GetEnumerator();
                    if (oldestStroke.MoveNext())
                    {
                        _strokeCache.Remove(oldestStroke.Current);
                    }
                }

                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    stroke.Draw(dc);
                }
                _strokeCache[stroke] = visual;
            }
        }

        /// <summary>
        /// 获取缓存的笔迹渲染结果
        /// </summary>
        public DrawingVisual GetCachedStroke(Stroke stroke)
        {
            if (!_enableCaching) return null;

            lock (_lockObject)
            {
                return _strokeCache.TryGetValue(stroke, out var visual) ? visual : null;
            }
        }

        /// <summary>
        /// 标记笔迹为脏状态
        /// </summary>
        public void MarkStrokeDirty(Stroke stroke)
        {
            lock (_lockObject)
            {
                _dirtyStrokes.Add(stroke);
                _strokeCache.Remove(stroke);
            }
        }

        /// <summary>
        /// 添加脏区域
        /// </summary>
        public void AddDirtyRegion(Rect region)
        {
            if (!_enableRegionRedraw) return;

            lock (_regionLock)
            {
                _dirtyRegions.Add(region);
            }
        }

        /// <summary>
        /// 获取所有脏区域
        /// </summary>
        public List<Rect> GetDirtyRegions()
        {
            lock (_regionLock)
            {
                var regions = new List<Rect>(_dirtyRegions);
                _dirtyRegions.Clear();
                return regions;
            }
        }

        /// <summary>
        /// 获取缓存的笔迹数量
        /// </summary>
        public int GetCachedStrokeCount()
        {
            lock (_lockObject)
            {
                return _strokeCache.Count;
            }
        }

        /// <summary>
        /// 批量重绘脏区域
        /// </summary>
        public async Task RedrawDirtyRegionsAsync(InkCanvas inkCanvas)
        {
            if (!_enableRegionRedraw) return;

            var regions = GetDirtyRegions();
            if (regions.Count == 0) return;

            await Task.Run(() =>
            {
                try
                {
                    // 合并重叠的区域
                    var mergedRegions = MergeOverlappingRegions(regions);
                    
                    // 在UI线程中重绘
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var region in mergedRegions)
                        {
                            // 触发区域重绘
                            inkCanvas.InvalidateVisual();
                        }
                    });
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"RedrawDirtyRegions error: {ex}", LogHelper.LogType.Error);
                }
            });
        }

        /// <summary>
        /// 合并重叠的区域
        /// </summary>
        private List<Rect> MergeOverlappingRegions(List<Rect> regions)
        {
            if (regions.Count <= 1) return regions;

            var merged = new List<Rect>();
            var used = new bool[regions.Count];

            for (int i = 0; i < regions.Count; i++)
            {
                if (used[i]) continue;

                var current = regions[i];
                used[i] = true;

                for (int j = i + 1; j < regions.Count; j++)
                {
                    if (used[j]) continue;

                    if (current.IntersectsWith(regions[j]))
                    {
                        current = Rect.Union(current, regions[j]);
                        used[j] = true;
                    }
                }

                merged.Add(current);
            }

            return merged;
        }

        /// <summary>
        /// 清理缓存
        /// </summary>
        public void ClearCache()
        {
            lock (_lockObject)
            {
                _strokeCache.Clear();
                _dirtyStrokes.Clear();
            }

            lock (_regionLock)
            {
                _dirtyRegions.Clear();
            }
        }

        /// <summary>
        /// 优化笔迹集合的渲染
        /// </summary>
        public void OptimizeStrokeCollection(InkCanvas inkCanvas, StrokeCollection strokes)
        {
            if (strokes == null || strokes.Count == 0) return;

            // 预缓存所有笔迹
            foreach (var stroke in strokes)
            {
                CacheStroke(stroke);
            }

            // 标记所有笔迹为脏状态，以便重新渲染
            foreach (var stroke in strokes)
            {
                MarkStrokeDirty(stroke);
            }
        }

        /// <summary>
        /// 异步优化渲染性能
        /// </summary>
        public async Task OptimizeRenderingAsync(InkCanvas inkCanvas)
        {
            await Task.Run(() =>
            {
                try
                {
                    // 在后台线程中预处理笔迹
                    var strokes = inkCanvas.Strokes;
                    foreach (var stroke in strokes)
                    {
                        // 预计算笔迹的边界
                        var bounds = stroke.GetBounds();
                        AddDirtyRegion(bounds);
                    }

                    // 在UI线程中应用优化
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // 触发重绘
                        inkCanvas.InvalidateVisual();
                    });
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"OptimizeRendering error: {ex}", LogHelper.LogType.Error);
                }
            });
        }
    }

    /// <summary>
    /// 笔迹渲染优化器
    /// </summary>
    public class StrokeRenderingOptimizer
    {
        private readonly RenderingOptimizer _renderer = RenderingOptimizer.Instance;
        private readonly PerformanceOptimizer _performance = PerformanceOptimizer.Instance;

        /// <summary>
        /// 优化笔迹渲染
        /// </summary>
        public async Task OptimizeStrokeRendering(InkCanvas inkCanvas, Stroke stroke)
        {
            if (stroke == null) return;

            await _performance.ProcessTransformAsync(() =>
            {
                try
                {
                    // 缓存笔迹
                    _renderer.CacheStroke(stroke);

                    // 添加脏区域
                    var bounds = stroke.GetBounds();
                    _renderer.AddDirtyRegion(bounds);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"OptimizeStrokeRendering error: {ex}", LogHelper.LogType.Error);
                }
            });
        }

        /// <summary>
        /// 批量优化笔迹渲染
        /// </summary>
        public async Task OptimizeStrokeCollectionRendering(InkCanvas inkCanvas, StrokeCollection strokes)
        {
            if (strokes == null || strokes.Count == 0) return;

            await _performance.ProcessTransformAsync(() =>
            {
                try
                {
                    _renderer.OptimizeStrokeCollection(inkCanvas, strokes);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"OptimizeStrokeCollectionRendering error: {ex}", LogHelper.LogType.Error);
                }
            });
        }

        /// <summary>
        /// 清理渲染资源
        /// </summary>
        public void Cleanup()
        {
            _renderer.ClearCache();
        }
    }
} 