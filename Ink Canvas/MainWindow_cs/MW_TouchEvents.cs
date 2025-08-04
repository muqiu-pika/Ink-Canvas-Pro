using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;
using Point = System.Windows.Point;

namespace Ink_Canvas
{
    public partial class MainWindow : Window
    {
        #region Multi-Touch

        bool isInMultiTouchMode = false;
        private void BorderMultiTouchMode_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isInMultiTouchMode)
            {
                // 退出多指触摸模式
                inkCanvas.StylusDown -= MainWindow_StylusDown;
                inkCanvas.StylusMove -= MainWindow_StylusMove;
                inkCanvas.StylusUp -= MainWindow_StylusUp;
                inkCanvas.TouchDown -= MainWindow_TouchDown;
                inkCanvas.TouchDown += Main_Grid_TouchDown;
                inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                isInMultiTouchMode = false;
                
                // 清理多指触摸相关的状态
                ClearMultiTouchState();
            }
            else
            {
                // 进入多指触摸模式
                inkCanvas.StylusDown += MainWindow_StylusDown;
                inkCanvas.StylusMove += MainWindow_StylusMove;
                inkCanvas.StylusUp += MainWindow_StylusUp;
                inkCanvas.TouchDown += MainWindow_TouchDown;
                inkCanvas.TouchDown -= Main_Grid_TouchDown;
                inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                isInMultiTouchMode = true;
            }
        }

        private void MainWindow_TouchDown(object sender, TouchEventArgs e)
        {
            try
            {
                if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint
                    || inkCanvas.EditingMode == InkCanvasEditingMode.EraseByStroke
                    || inkCanvas.EditingMode == InkCanvasEditingMode.Select) return;

                if (!isHidingSubPanelsWhenInking)
                {
                    isHidingSubPanelsWhenInking = true;
                    HideSubPanels(); // 书写时自动隐藏二级菜单
                }

                double boundWidth = e.GetTouchPoint(null).Bounds.Width;
                if ((Settings.Advanced.TouchMultiplier != 0 || !Settings.Advanced.IsSpecialScreen) //启用特殊屏幕且触摸倍数为 0 时禁用橡皮
                    && (boundWidth > BoundsWidth))
                {
                    if (drawingShapeMode == 0 && forceEraser) return;
                    double EraserThresholdValue = Settings.Startup.IsEnableNibMode ? Settings.Advanced.NibModeBoundsWidthThresholdValue : Settings.Advanced.FingerModeBoundsWidthThresholdValue;
                    if (boundWidth > BoundsWidth * EraserThresholdValue)
                    {
                        boundWidth *= (Settings.Startup.IsEnableNibMode ? Settings.Advanced.NibModeBoundsWidthEraserSize : Settings.Advanced.FingerModeBoundsWidthEraserSize);
                        if (Settings.Advanced.IsSpecialScreen) boundWidth *= Settings.Advanced.TouchMultiplier;
                        inkCanvas.EraserShape = new EllipseStylusShape(boundWidth, boundWidth);
                        TouchDownPointsList[e.TouchDevice.Id] = InkCanvasEditingMode.EraseByPoint;
                        inkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
                    }
                    else
                    {
                        inkCanvas.EraserShape = new EllipseStylusShape(5, 5);
                        inkCanvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
                    }
                }
                else
                {
                    inkCanvas.EraserShape = forcePointEraser ? new EllipseStylusShape(50, 50) : new EllipseStylusShape(5, 5);
                    TouchDownPointsList[e.TouchDevice.Id] = InkCanvasEditingMode.None;
                    inkCanvas.EditingMode = InkCanvasEditingMode.None;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"Touch down error: {ex}", LogHelper.LogType.Error);
            }
        }

        private void MainWindow_StylusDown(object sender, StylusDownEventArgs e)
        {
            try
            {
                if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint
                    || inkCanvas.EditingMode == InkCanvasEditingMode.EraseByStroke
                    || inkCanvas.EditingMode == InkCanvasEditingMode.Select) return;

                TouchDownPointsList[e.StylusDevice.Id] = InkCanvasEditingMode.None;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"Stylus down error: {ex}", LogHelper.LogType.Error);
            }
        }

        // 渲染优化器实例
        private readonly SimplePerformanceOptimizer _renderingOptimizer = SimplePerformanceOptimizer.Instance;

        private void MainWindow_StylusUp(object sender, StylusEventArgs e)
        {
            try
            {
                if (e.StylusDevice.TabletDevice.Type == TabletDeviceType.Stylus)
                {
                    // 数位板 TabletDeviceType.Stylus
                }
                else
                {
                    try
                    {
                        // 触摸屏 TabletDeviceType.Touch 
                        var strokeVisual = GetStrokeVisual(e.StylusDevice.Id);
                        if (strokeVisual?.Stroke != null)
                        {
                            var stroke = strokeVisual.Stroke;
                            
                            // 先移除预览画布，避免重复显示
                            var visualCanvas = GetVisualCanvas(e.StylusDevice.Id);
                            if (visualCanvas != null && inkCanvas.Children.Contains(visualCanvas))
                            {
                                inkCanvas.Children.Remove(visualCanvas);
                            }
                            
                            // 添加笔迹到画布
                            inkCanvas.Strokes.Add(stroke);
                            
                            // 优化：异步优化笔迹渲染
                            _renderingOptimizer.ProcessTransformAsync(() =>
                            {
                                // 简化的渲染优化
                                var bounds = stroke.GetBounds();
                            }).ConfigureAwait(false);
                            
                            // 立即进行形状识别，避免延迟导致的重复笔迹
                            inkCanvas_StrokeCollected(inkCanvas, new InkCanvasStrokeCollectedEventArgs(stroke));
                        }
                    }
                    catch(Exception ex) {
                        LogHelper.WriteLogToFile($"Touch stylus up error: {ex}", LogHelper.LogType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"Stylus up error: {ex}", LogHelper.LogType.Error);
            }
            
            // 清理资源
            try
            {
                StrokeVisualList.Remove(e.StylusDevice.Id);
                VisualCanvasList.Remove(e.StylusDevice.Id);
                TouchDownPointsList.Remove(e.StylusDevice.Id);
                
                // 如果所有列表都为空，清理内存
                if (StrokeVisualList.Count == 0 && VisualCanvasList.Count == 0 && TouchDownPointsList.Count == 0)
                {
                    StrokeVisualList.Clear();
                    VisualCanvasList.Clear();
                    TouchDownPointsList.Clear();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"Cleanup error: {ex}", LogHelper.LogType.Error);
            }
        }

        private void MainWindow_StylusMove(object sender, StylusEventArgs e)
        {
            try
            {
                if (GetTouchDownPointsList(e.StylusDevice.Id) != InkCanvasEditingMode.None) return;
                
                // 检查按钮状态
                try
                {
                    if (e.StylusDevice.StylusButtons[1].StylusButtonState == StylusButtonState.Down) return;
                }
                catch { }
                
                var strokeVisual = GetStrokeVisual(e.StylusDevice.Id);
                if (strokeVisual == null) return;
                
                var stylusPointCollection = e.GetStylusPoints(this);
                if (stylusPointCollection.Count == 0) return;
                
                foreach (var stylusPoint in stylusPointCollection)
                {
                    strokeVisual.Add(new StylusPoint(stylusPoint.X, stylusPoint.Y, stylusPoint.PressureFactor));
                }
                
                // 优化：减少重绘频率
                if (stylusPointCollection.Count > 0)
                {
                    strokeVisual.Redraw();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"Stylus move error: {ex}", LogHelper.LogType.Error);
            }
        }

        private StrokeVisual GetStrokeVisual(int id)
        {
            if (StrokeVisualList.TryGetValue(id, out var visual))
            {
                return visual;
            }

            var strokeVisual = new StrokeVisual(inkCanvas.DefaultDrawingAttributes.Clone());
            StrokeVisualList[id] = strokeVisual;
            var visualCanvas = new VisualCanvas(strokeVisual);
            VisualCanvasList[id] = visualCanvas;
            inkCanvas.Children.Add(visualCanvas);

            return strokeVisual;
        }

        private VisualCanvas GetVisualCanvas(int id)
        {
            if (VisualCanvasList.TryGetValue(id, out var visualCanvas))
            {
                return visualCanvas;
            }
            return null;
        }

        private InkCanvasEditingMode GetTouchDownPointsList(int id)
        {
            if (TouchDownPointsList.TryGetValue(id, out var inkCanvasEditingMode))
            {
                return inkCanvasEditingMode;
            }
            return inkCanvas.EditingMode;
        }

        private Dictionary<int, InkCanvasEditingMode> TouchDownPointsList { get; } = new Dictionary<int, InkCanvasEditingMode>();
        private Dictionary<int, StrokeVisual> StrokeVisualList { get; } = new Dictionary<int, StrokeVisual>();
        private Dictionary<int, VisualCanvas> VisualCanvasList { get; } = new Dictionary<int, VisualCanvas>();

        #endregion

        int lastTouchDownTime = 0, lastTouchUpTime = 0;

        Point iniP = new Point(0, 0);
        bool isLastTouchEraser = false;
        private bool forcePointEraser = true;

        private void Main_Grid_TouchDown(object sender, TouchEventArgs e)
        {
            if (!isHidingSubPanelsWhenInking)
            {
                isHidingSubPanelsWhenInking = true;
                HideSubPanels(); // 书写时自动隐藏二级菜单
            }

            if (NeedUpdateIniP())
            {
                iniP = e.GetTouchPoint(inkCanvas).Position;
            }
            if (drawingShapeMode == 9 && isFirstTouchCuboid == false)
            {
                MouseTouchMove(iniP);
            }
            inkCanvas.Opacity = 1;
            double boundsWidth = GetTouchBoundWidth(e);
            if ((Settings.Advanced.TouchMultiplier != 0 || !Settings.Advanced.IsSpecialScreen) //启用特殊屏幕且触摸倍数为 0 时禁用橡皮
                && (boundsWidth > BoundsWidth))
            {
                isLastTouchEraser = true;
                if (drawingShapeMode == 0 && forceEraser) return;
                double EraserThresholdValue = Settings.Startup.IsEnableNibMode ? Settings.Advanced.NibModeBoundsWidthThresholdValue : Settings.Advanced.FingerModeBoundsWidthThresholdValue;
                if (boundsWidth > BoundsWidth * EraserThresholdValue)
                {
                    boundsWidth *= (Settings.Startup.IsEnableNibMode ? Settings.Advanced.NibModeBoundsWidthEraserSize : Settings.Advanced.FingerModeBoundsWidthEraserSize);
                    if (Settings.Advanced.IsSpecialScreen) boundsWidth *= Settings.Advanced.TouchMultiplier;
                    inkCanvas.EraserShape = new EllipseStylusShape(boundsWidth, boundsWidth);
                    inkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
                }
                else
                {
                    if (BtnPPTSlideShowEnd.Visibility == Visibility.Visible && inkCanvas.Strokes.Count == 0 && Settings.PowerPointSettings.IsEnableFingerGestureSlideShowControl)
                    {
                        isLastTouchEraser = false;
                        inkCanvas.EditingMode = InkCanvasEditingMode.GestureOnly;
                        inkCanvas.Opacity = 0.1;
                    }
                    else
                    {
                        inkCanvas.EraserShape = new EllipseStylusShape(5, 5);
                        inkCanvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
                    }
                }
            }
            else
            {
                isLastTouchEraser = false;
                inkCanvas.EraserShape = forcePointEraser ? new EllipseStylusShape(50, 50) : new EllipseStylusShape(5, 5);
                if (forceEraser) return;
                inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
            }
        }

        public double GetTouchBoundWidth(TouchEventArgs e)
        {
            var args = e.GetTouchPoint(null).Bounds;
            if (!Settings.Advanced.IsQuadIR) return args.Width;
            else return Math.Sqrt(args.Width * args.Height); //四边红外
        }

        //记录触摸设备ID
        private List<int> dec = new List<int>();
        //中心点
        Point centerPoint;
        InkCanvasEditingMode lastInkCanvasEditingMode = InkCanvasEditingMode.Ink;
        bool isSingleFingerDragMode = false;

        private void inkCanvas_PreviewTouchDown(object sender, TouchEventArgs e)
        {
            try
            {
                dec.Add(e.TouchDevice.Id);
                
                // 设备1个的时候，记录中心点
                if (dec.Count == 1)
                {
                    TouchPoint touchPoint = e.GetTouchPoint(inkCanvas);
                    centerPoint = touchPoint.Position;

                    // 记录第一根手指点击时的 StrokeCollection
                    lastTouchDownStrokeCollection = inkCanvas.Strokes.Clone();
                }
                
                // 设备两个及两个以上，将画笔功能关闭
                if (dec.Count > 1 || isSingleFingerDragMode || !Settings.Gesture.IsEnableTwoFingerGesture)
                {
                    if (isInMultiTouchMode || !Settings.Gesture.IsEnableTwoFingerGesture) return;
                    
                    if (inkCanvas.EditingMode != InkCanvasEditingMode.None && inkCanvas.EditingMode != InkCanvasEditingMode.Select)
                    {
                        lastInkCanvasEditingMode = inkCanvas.EditingMode;
                        inkCanvas.EditingMode = InkCanvasEditingMode.None;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"Preview touch down error: {ex}", LogHelper.LogType.Error);
            }
        }

        private void inkCanvas_PreviewTouchUp(object sender, TouchEventArgs e)
        {
            try
            {
                // 手势完成后切回之前的状态
                if (dec.Count > 1)
                {
                    if (inkCanvas.EditingMode == InkCanvasEditingMode.None)
                    {
                        inkCanvas.EditingMode = lastInkCanvasEditingMode;
                    }
                }
                
                dec.Remove(e.TouchDevice.Id);
                inkCanvas.Opacity = 1;
                
                if (dec.Count == 0)
                {
                    if (lastTouchDownStrokeCollection.Count() != inkCanvas.Strokes.Count() &&
                        !(drawingShapeMode == 9 && !isFirstTouchCuboid))
                    {
                        int whiteboardIndex = CurrentWhiteboardIndex;
                        if (currentMode == 0)
                        {
                            whiteboardIndex = 0;
                        }
                        strokeCollections[whiteboardIndex] = lastTouchDownStrokeCollection;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"Preview touch up error: {ex}", LogHelper.LogType.Error);
            }
        }
        private void inkCanvas_ManipulationStarting(object sender, ManipulationStartingEventArgs e)
        {
            e.Mode = ManipulationModes.All;
        }

        private void inkCanvas_ManipulationInertiaStarting(object sender, ManipulationInertiaStartingEventArgs e)
        {

        }

        private void Main_Grid_ManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
        {
            if (e.Manipulators.Count() == 0)
            {
                if (forceEraser) return;
                inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
            }
        }

        // 简化的性能优化器实例
        private readonly SimplePerformanceOptimizer _performanceOptimizer = SimplePerformanceOptimizer.Instance;
        private readonly SimpleTransformManager _transformManager = new SimpleTransformManager();

        private void Main_Grid_ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
        {
            if (isInMultiTouchMode || !Settings.Gesture.IsEnableTwoFingerGesture) return;
            if ((dec.Count >= 2 && (Settings.PowerPointSettings.IsEnableTwoFingerGestureInPresentationMode || BtnPPTSlideShowEnd.Visibility != Visibility.Visible)) || isSingleFingerDragMode)
            {
                // 使用简化的节流处理变换
                _performanceOptimizer.SimpleThrottle(() => ProcessManipulationDelta(e));
            }
        }

        private void ProcessManipulationDelta(ManipulationDeltaEventArgs e)
        {
            try
            {
                Matrix m = _performanceOptimizer.GetMatrix();
                ManipulationDelta md = e.DeltaManipulation;
                
                // Translation
                Vector trans = md.Translation;
                
                // Rotate, Scale
                if (Settings.Gesture.IsEnableTwoFingerGestureTranslateOrRotation)
                {
                    double rotate = md.Rotation;
                    Vector scale = md.Scale;
                    Point center = GetMatrixTransformCenterPoint(e.ManipulationOrigin, e.Source as FrameworkElement);
                    
                    if (Settings.Gesture.IsEnableTwoFingerZoom)
                        m.ScaleAt(scale.X, scale.Y, center.X, center.Y);
                    if (Settings.Gesture.IsEnableTwoFingerRotation)
                        m.RotateAt(rotate, center.X, center.Y);
                    if (Settings.Gesture.IsEnableTwoFingerTranslate)
                        m.Translate(trans.X, trans.Y);
                    
                    // 优化：只处理选中的元素，而不是所有元素
                    List<UIElement> elements = inkCanvas.GetSelectedElements().Count > 0 
                        ? InkCanvasElementsHelper.GetSelectedElements(inkCanvas) 
                        : InkCanvasElementsHelper.GetAllElements(inkCanvas);
                    
                    _transformManager.ApplyTransformToElements(elements, m);
                }
                
                // 优化：只处理选中的笔迹，而不是所有笔迹
                StrokeCollection targetStrokes = inkCanvas.GetSelectedStrokes().Count > 0 
                    ? inkCanvas.GetSelectedStrokes() 
                    : inkCanvas.Strokes;
                
                if (Settings.Gesture.IsEnableTwoFingerZoom)
                {
                    _transformManager.ApplyTransformToStrokes(targetStrokes, m, true, md.Scale);
                }
                else
                {
                    _transformManager.ApplyTransformToStrokes(targetStrokes, m);
                }
                
                // 优化：延迟更新圆形对象，避免频繁计算
                try
                {
                    if (circles != null && circles.Count > 0)
                    {
                        UpdateCirclesAsync();
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"Update circles error: {ex}", LogHelper.LogType.Error);
                }
                
                // 提交所有变换
                _transformManager.CommitTransforms();
                
                // 返回矩阵到对象池
                _performanceOptimizer.ReturnMatrix(m);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"ManipulationDelta error: {ex}", LogHelper.LogType.Error);
            }
        }

        private void UpdateCirclesAsync()
        {
            _performanceOptimizer.ProcessTransformAsync(() =>
            {
                try
                {
                    // 检查circles变量是否存在
                    if (circles != null && circles.Count > 0)
                    {
                        foreach (Circle circle in circles)
                        {
                            if (circle?.Stroke != null && circle.Stroke.StylusPoints.Count > 1)
                            {
                                circle.R = GetDistance(circle.Stroke.StylusPoints[0].ToPoint(), 
                                                     circle.Stroke.StylusPoints[circle.Stroke.StylusPoints.Count / 2].ToPoint()) / 2;
                                circle.Centroid = new Point(
                                    (circle.Stroke.StylusPoints[0].X + circle.Stroke.StylusPoints[circle.Stroke.StylusPoints.Count / 2].X) / 2,
                                    (circle.Stroke.StylusPoints[0].Y + circle.Stroke.StylusPoints[circle.Stroke.StylusPoints.Count / 2].Y) / 2
                                );
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"UpdateCirclesAsync error: {ex}", LogHelper.LogType.Error);
                }
            }).ConfigureAwait(false);
        }

        private void ClearMultiTouchState()
        {
            try
            {
                // 清理所有预览画布
                foreach (var visualCanvas in VisualCanvasList.Values)
                {
                    if (inkCanvas.Children.Contains(visualCanvas))
                    {
                        inkCanvas.Children.Remove(visualCanvas);
                    }
                }
                
                // 清理所有列表
                StrokeVisualList.Clear();
                VisualCanvasList.Clear();
                TouchDownPointsList.Clear();
                dec.Clear();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"ClearMultiTouchState error: {ex}", LogHelper.LogType.Error);
            }
        }
    }
}
