using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;

namespace Ink_Canvas.Helpers
{
    public static class LineRecognitionHelper
    {
        /// <summary>
        /// 判断给定的笔划是否为直线
        /// </summary>
        /// <param name="stroke">要识别的笔划</param>
        /// <param name="tolerance">容差值（像素）</param>
        /// <param name="minLength">最小长度阈值</param>
        /// <returns>是否为直线</returns>
        public static bool IsLine(Stroke stroke, double tolerance = 15.0, double minLength = 100.0)
        {
            if (stroke == null || stroke.StylusPoints.Count < 2)
                return false;

            var points = stroke.StylusPoints.Select(p => new Point(p.X, p.Y)).ToList();
            
            // 检查线条长度
            double lineLength = GetDistance(points.First(), points.Last());
            if (lineLength < minLength)
                return false;

            // 计算点到直线的距离
            Point start = points.First();
            Point end = points.Last();
            
            // 如果只有两个点，直接认为是直线
            if (points.Count == 2)
                return true;

            double maxDeviation = 0;
            int fluctuationCount = 0;
            
            // 计算所有点到直线的距离
            List<double> distances = new List<double>();
            for (int i = 1; i < points.Count - 1; i++)
            {
                double distance = PointToLineDistance(start, end, points[i]);
                distances.Add(Math.Abs(distance));
                maxDeviation = Math.Max(maxDeviation, Math.Abs(distance));
            }

            // 如果最大偏差超过容差，不是直线
            if (maxDeviation > tolerance)
                return false;

            // 检查波动次数
            double avgDistance = distances.Average();
            double threshold = tolerance * 0.5;
            
            bool wasAbove = false;
            for (int i = 0; i < distances.Count; i++)
            {
                bool isAbove = distances[i] > avgDistance + threshold;
                if (isAbove != wasAbove && i > 0)
                {
                    fluctuationCount++;
                }
                wasAbove = isAbove;
            }

            // 限制波动次数不超过4次
            return fluctuationCount <= 4;
        }

        /// <summary>
        /// 计算两点之间的距离
        /// </summary>
        private static double GetDistance(Point p1, Point p2)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// 计算点到线段的距离
        /// </summary>
        private static double PointToLineDistance(Point lineStart, Point lineEnd, Point point)
        {
            double A = lineEnd.Y - lineStart.Y;
            double B = lineStart.X - lineEnd.X;
            double C = lineEnd.X * lineStart.Y - lineStart.X * lineEnd.Y;
            
            double denominator = Math.Sqrt(A * A + B * B);
            if (denominator < 1e-10)
                return GetDistance(point, lineStart);
            
            double distance = Math.Abs(A * point.X + B * point.Y + C) / denominator;
            
            // 检查投影点是否在线段内
            double t = ((point.X - lineStart.X) * (lineEnd.X - lineStart.X) + 
                       (point.Y - lineStart.Y) * (lineEnd.Y - lineStart.Y)) / 
                       (GetDistance(lineStart, lineEnd) * GetDistance(lineStart, lineEnd));
            
            if (t < 0) return GetDistance(point, lineStart);
            if (t > 1) return GetDistance(point, lineEnd);
            
            return distance;
        }

        /// <summary>
        /// 创建直线笔划
        /// </summary>
        public static Stroke CreateLineStroke(Point start, Point end, DrawingAttributes attributes)
        {
            var points = new List<Point> { start, end };
            var stylusPoints = new StylusPointCollection(points);
            return new Stroke(stylusPoints) { DrawingAttributes = attributes.Clone() };
        }

        /// <summary>
        /// 获取笔划的边界矩形
        /// </summary>
        public static Rect GetStrokeBounds(Stroke stroke)
        {
            if (stroke == null || stroke.StylusPoints.Count == 0)
                return Rect.Empty;

            double minX = double.MaxValue;
            double maxX = double.MinValue;
            double minY = double.MaxValue;
            double maxY = double.MinValue;

            foreach (var point in stroke.StylusPoints)
            {
                minX = Math.Min(minX, point.X);
                maxX = Math.Max(maxX, point.X);
                minY = Math.Min(minY, point.Y);
                maxY = Math.Max(maxY, point.Y);
            }

            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }
    }
}