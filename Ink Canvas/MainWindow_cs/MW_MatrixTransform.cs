using Ink_Canvas.Helpers;
using System.Windows;
using System;

namespace Ink_Canvas
{
    public partial class MainWindow : Window
    {
        private Point GetMatrixTransformCenterPoint(Point gestureOperationCenterPoint, FrameworkElement fe)
        {
            try
            {
                if (fe == null) return new Point(0, 0);
                
                Point canvasCenterPoint = new Point(fe.ActualWidth / 2, fe.ActualHeight / 2);
                if (!isLoaded) return canvasCenterPoint;
                
                if (Settings.Gesture.MatrixTransformCenterPoint == MatrixTransformCenterPointOptions.CanvasCenterPoint)
                {
                    return canvasCenterPoint;
                }
                else if (Settings.Gesture.MatrixTransformCenterPoint == MatrixTransformCenterPointOptions.GestureOperationCenterPoint)
                {
                    return gestureOperationCenterPoint;
                }
                else if (Settings.Gesture.MatrixTransformCenterPoint == MatrixTransformCenterPointOptions.SelectedElementsCenterPoint)
                {
                    return InkCanvasElementsHelper.GetAllElementsBoundsCenterPoint(inkCanvas);
                }
                return canvasCenterPoint;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"GetMatrixTransformCenterPoint error: {ex}", LogHelper.LogType.Error);
                return new Point(0, 0);
            }
        }
    }
}
