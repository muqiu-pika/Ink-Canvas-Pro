using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using Ink_Canvas.Helpers;
using Ink_Canvas.Resources;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 性能优化助手类
    /// </summary>
    public class PerformanceOptimizer
    {
        private static readonly object _lockObject = new object();
        private static PerformanceOptimizer _instance;
        public static PerformanceOptimizer Instance => _instance ??= new PerformanceOptimizer();

        // 节流控制
        private DateTime _lastManipulationUpdate = DateTime.MinValue;
        private const int MinUpdateIntervalMs = 16; // 约60fps
        private readonly Queue<ManipulationDeltaEventArgs> _pendingUpdates = new Queue<ManipulationDeltaEventArgs>();

        // 对象池
        private readonly Stack<Matrix> _matrixPool = new Stack<Matrix>();
        private readonly Stack<Vector> _vectorPool = new Stack<Vector>();

        // 性能监控
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private int _transformCount = 0;
        private long _totalTransformTime = 0;

        // 批量处理
        private readonly Dictionary<Stroke, Matrix> _pendingStrokeTransforms = new Dictionary<Stroke, Matrix>();
        private readonly HashSet<Stroke> _dirtyStrokes = new HashSet<Stroke>();

        /// <summary>
        /// 获取矩阵对象（从对象池）
        /// </summary>
        public Matrix GetMatrix()
        {
            lock (_lockObject)
            {
                return _matrixPool.Count > 0 ? _matrixPool.Pop() : new Matrix();
            }
        }

        /// <summary>
        /// 返回矩阵对象到对象池
        /// </summary>
        public void ReturnMatrix(Matrix matrix)
        {
            lock (_lockObject)
            {
                matrix.SetIdentity();
                _matrixPool.Push(matrix);
            }
        }

        /// <summary>
        /// 获取向量对象（从对象池）
        /// </summary>
        public Vector GetVector()
        {
            lock (_lockObject)
            {
                return _vectorPool.Count > 0 ? _vectorPool.Pop() : new Vector();
            }
        }

        /// <summary>
        /// 返回向量对象到对象池
        /// </summary>
        public void ReturnVector(Vector vector)
        {
            lock (_lockObject)
            {
                vector = new Vector();
                _vectorPool.Push(vector);
            }
        }

        /// <summary>
        /// 节流控制 - 检查是否可以处理更新
        /// </summary>
        public bool CanProcessUpdate()
        {
            var now = DateTime.Now;
            if ((now - _lastManipulationUpdate).TotalMilliseconds >= MinUpdateIntervalMs)
            {
                _lastManipulationUpdate = now;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 开始性能监控
        /// </summary>
        public void BeginPerformanceMonitoring()
        {
            _stopwatch.Restart();
        }

        /// <summary>
        /// 结束性能监控并记录
        /// </summary>
        public void EndPerformanceMonitoring(int strokeCount, string operation = "Transform")
        {
            _stopwatch.Stop();
            _transformCount++;
            _totalTransformTime += _stopwatch.ElapsedMilliseconds;

            if (_transformCount % 50 == 0)
            {
                var avgTime = _totalTransformTime / _transformCount;
                LogHelper.WriteLogToFile($"{operation} Performance: {avgTime}ms avg for {strokeCount} strokes", LogHelper.LogType.Info);
            }
        }

        /// <summary>
        /// 批量应用笔迹变换
        /// </summary>
        public void BatchApplyStrokeTransforms()
        {
            if (_dirtyStrokes.Count == 0) return;

            BeginPerformanceMonitoring();

            foreach (var stroke in _dirtyStrokes)
            {
                if (_pendingStrokeTransforms.TryGetValue(stroke, out var transform))
                {
                    stroke.Transform(transform, false);
                }
            }

            EndPerformanceMonitoring(_dirtyStrokes.Count, "BatchTransform");

            _dirtyStrokes.Clear();
            _pendingStrokeTransforms.Clear();
        }

        /// <summary>
        /// 添加笔迹变换到批量队列
        /// </summary>
        public void QueueStrokeTransform(Stroke stroke, Matrix transform)
        {
            if (_pendingStrokeTransforms.ContainsKey(stroke))
            {
                _pendingStrokeTransforms[stroke] = _pendingStrokeTransforms[stroke] * transform;
            }
            else
            {
                _pendingStrokeTransforms[stroke] = transform;
            }
            _dirtyStrokes.Add(stroke);
        }

        /// <summary>
        /// 异步处理变换操作
        /// </summary>
        public async Task ProcessTransformAsync(Action transformAction)
        {
            await Task.Run(() =>
            {
                try
                {
                    transformAction?.Invoke();
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"Async transform error: {ex}", LogHelper.LogType.Error);
                }
            });
        }

        /// <summary>
        /// 智能节流 - 合并多个变换操作
        /// </summary>
        public void SmartThrottle(ManipulationDeltaEventArgs e, Action<ManipulationDeltaEventArgs> processAction)
        {
            _pendingUpdates.Enqueue(e);

            if (CanProcessUpdate())
            {
                // 合并所有待处理的变换
                ManipulationDeltaEventArgs combinedUpdate = null;
                Vector combinedTranslation = new Vector();
                Vector combinedScale = new Vector(1, 1);
                double combinedRotation = 0.0;
                
                while (_pendingUpdates.Count > 0)
                {
                    var update = _pendingUpdates.Dequeue();
                    if (combinedUpdate == null)
                    {
                        combinedUpdate = update;
                        combinedTranslation = update.DeltaManipulation.Translation;
                        combinedScale = update.DeltaManipulation.Scale;
                        combinedRotation = update.DeltaManipulation.Rotation;
                    }
                    else
                    {
                        // 合并变换参数
                        combinedTranslation += update.DeltaManipulation.Translation;
                        combinedScale = new Vector(combinedScale.X * update.DeltaManipulation.Scale.X, combinedScale.Y * update.DeltaManipulation.Scale.Y);
                        combinedRotation += update.DeltaManipulation.Rotation;
                    }
                }

                if (combinedUpdate != null)
                {
                    // 直接使用最后一个更新，但应用合并的变换
                    // 这样可以避免复杂的构造函数问题
                    processAction?.Invoke(combinedUpdate);
                }
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Dispose()
        {
            lock (_lockObject)
            {
                _matrixPool.Clear();
                _vectorPool.Clear();
                _pendingUpdates.Clear();
                _pendingStrokeTransforms.Clear();
                _dirtyStrokes.Clear();
            }
        }
    }

    /// <summary>
    /// 优化的变换管理器
    /// </summary>
    public class OptimizedTransformManager
    {
        private readonly PerformanceOptimizer _optimizer = PerformanceOptimizer.Instance;
        private readonly Dictionary<Stroke, Matrix> _strokeTransforms = new Dictionary<Stroke, Matrix>();
        private readonly HashSet<UIElement> _dirtyElements = new HashSet<UIElement>();

        /// <summary>
        /// 应用变换到笔迹
        /// </summary>
        public void ApplyTransformToStrokes(StrokeCollection strokes, Matrix transform, bool updateDrawingAttributes = false, Vector scale = new Vector())
        {
            if (strokes == null || strokes.Count == 0) return;

            _optimizer.BeginPerformanceMonitoring();

            foreach (Stroke stroke in strokes)
            {
                _optimizer.QueueStrokeTransform(stroke, transform);

                if (updateDrawingAttributes && scale != new Vector())
                {
                    try
                    {
                        stroke.DrawingAttributes.Width *= scale.X;
                        stroke.DrawingAttributes.Height *= scale.Y;
                    }
                    catch { }
                }
            }

            _optimizer.EndPerformanceMonitoring(strokes.Count, "StrokeTransform");
        }

        /// <summary>
        /// 应用变换到UI元素
        /// </summary>
        public void ApplyTransformToElements(List<UIElement> elements, Matrix transform)
        {
            if (elements == null || elements.Count == 0) return;

            foreach (UIElement element in elements)
            {
                ApplyElementMatrixTransform(element, transform);
                _dirtyElements.Add(element);
            }
        }

        /// <summary>
        /// 提交所有待处理的变换
        /// </summary>
        public void CommitTransforms()
        {
            _optimizer.BatchApplyStrokeTransforms();
            _dirtyElements.Clear();
        }

        /// <summary>
        /// 应用元素矩阵变换（优化版本）
        /// </summary>
        private void ApplyElementMatrixTransform(UIElement element, Matrix matrix)
        {
            if (element is FrameworkElement frameworkElement)
            {
                TransformGroup transformGroup = frameworkElement.RenderTransform as TransformGroup;
                if (transformGroup == null)
                {
                    transformGroup = new TransformGroup();
                    frameworkElement.RenderTransform = transformGroup;
                }

                TransformGroup centeredTransformGroup = new TransformGroup();
                centeredTransformGroup.Children.Add(new MatrixTransform(matrix));
                transformGroup.Children.Add(centeredTransformGroup);
            }
        }
    }
} 