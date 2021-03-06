﻿//The MIT License(MIT)

//Copyright(c) 2015 Alberto Rodriguez

//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.ComponentModel;
using System.Linq;
using System.Windows.Shapes;
using System.Windows.Threading;
using LiveCharts.CoreComponents;
using LiveCharts.Shapes;
using LiveCharts.TypeConverters;

namespace LiveCharts
{
    public class PieSeries : Series
    {
        private int animationSpeed = 500;
        private bool _isPrimitive;

        public PieSeries()
        {
            SetValue(StrokeProperty, new SolidColorBrush(Colors.White));
            SetValue(ForegroundProperty, new SolidColorBrush(Colors.White));
        }

        public static readonly DependencyProperty BrushesProperty = DependencyProperty.Register(
            "Brushes", typeof (Brush[]), typeof (PieSeries), new PropertyMetadata(default(Brush[])));

        [TypeConverter(typeof (BrushesCollectionConverter))]
        public Brush[] Brushes
        {
            get { return (Brush[]) GetValue(BrushesProperty); }
            set { SetValue(BrushesProperty, value); }
        }

        public override void Plot(bool animate = true)
        {
            if (Visibility != Visibility.Visible) return;
            var pChart = Chart as PieChart;
            if (pChart == null) return;
            if (pChart.PieTotalSum <= 0) return;
            var rotated = 0d;

            Chart.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            var minDimension = Chart.PlotArea.Width < Chart.PlotArea.Height
                ? Chart.PlotArea.Width
                : Chart.PlotArea.Height;
            minDimension -= pChart.DrawPadding;
            minDimension = minDimension < pChart.DrawPadding ? pChart.DrawPadding : minDimension;

            var isFist = true;

            var f = Chart.GetFormatter(Chart.AxisY);
            var pie = (PieChart) Chart;

            var visuals = Values.Points.ToDictionary(x => (int) x.X, GetVisual);
            var allNew = visuals.All(x => x.Value.IsNew);

            foreach (var point in Values.Points)
            {
                var participation = point.Y/pChart.PieTotalSum;
                if (isFist)
                {
                    rotated = participation*-.5  + (pie.PieRotation/360);
                    isFist = false;
                }

                var visual = visuals[(int) point.X];

                visual.PointShape.Radius = minDimension/2;

                Canvas.SetTop(visual.PointShape, Chart.PlotArea.Height / 2);
                Canvas.SetLeft(visual.PointShape, Chart.PlotArea.Width / 2);

                if (!Chart.DisableAnimation)
                {
                    var wa = new DoubleAnimation
                    {
                        From = visual.IsNew ? 0 : visual.PointShape.WedgeAngle,
                        To = 360 * participation,
                        Duration = TimeSpan.FromMilliseconds(animationSpeed)
                    };
                    var ra = new DoubleAnimation
                    {
                        From =
                            visual.IsNew
                                ? (allNew ? 0 : 360*rotated + 360*participation)
                                : visual.PointShape.RotationAngle,
                        To = 360*rotated,
                        Duration = TimeSpan.FromMilliseconds(animationSpeed)
                    };
                    visual.PointShape.BeginAnimation(PieSlice.WedgeAngleProperty, wa);
                    visual.PointShape.BeginAnimation(PieSlice.RotationAngleProperty, ra);
                }

                if (DataLabels)
                {
                    var tb = BuildATextBlock(0);
                    tb.Text = f(point.Y);

                    var hypo = ((minDimension / 2) + (pChart.InnerRadius > 10 ? pChart.InnerRadius : 10)) / 2;
                    var gamma = participation * 360 / 2 + rotated * 360;
                    var cp = new Point(hypo * Math.Sin(gamma * (Math.PI / 180)), hypo * Math.Cos(gamma * (Math.PI / 180)));

                    tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

                    Canvas.SetTop(tb, Chart.PlotArea.Height / 2 - cp.Y - tb.DesiredSize.Height * .5);
                    Canvas.SetLeft(tb, cp.X + Chart.PlotArea.Width / 2 - tb.DesiredSize.Width * .5);
                    Panel.SetZIndex(tb, int.MaxValue - 1);
                    //because math is kind of complex to detetrmine if label fits inside the slide, by now we 
                    //will just add it if participation > 5% ToDo: the math!
                    if (participation > .05 && Chart.AxisY.ShowLabels)
                    {
                        Chart.Canvas.Children.Add(tb);
                        Chart.Shapes.Add(tb);
                        tb.Visibility = Visibility.Hidden;
                        if (!Chart.DisableAnimation)
                        {
                            var t = new DispatcherTimer {Interval = TimeSpan.FromMilliseconds(animationSpeed)};
                            t.Tick += (sender, args) =>
                            {
                                tb.Visibility = Visibility.Visible;
                                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(animationSpeed));
                                tb.BeginAnimation(OpacityProperty, fadeIn);
                                t.Stop();
                            };
                            t.Start();
                        }
                        else
                        {
                            tb.Visibility = Visibility.Visible;
                        }
                    }
                }

                if (visual.IsNew)
                {
                    Chart.ShapesMapper.Add(new ShapeMap
                    {
                        Series = this,
                        HoverShape = visual.HoverShape,
                        Shape = visual.PointShape,
                        ChartPoint = point
                    });
                    Chart.Canvas.Children.Add(visual.PointShape);
                    //Chart.Canvas.Children.Add(visual.HoverShape);
                    Shapes.Add(visual.PointShape);
                    //Shapes.Add(visual.HoverShape);
                    //Panel.SetZIndex(visual.HoverShape, int.MaxValue);
                    Panel.SetZIndex(visual.PointShape, int.MaxValue - 2);
                    visual.PointShape.MouseDown += Chart.DataMouseDown;
                    visual.PointShape.MouseEnter += Chart.DataMouseEnter;
                    visual.PointShape.MouseLeave += Chart.DataMouseLeave;
                }
                rotated += participation;
            }
        }

        internal override void Erase(bool force = false)
        {
            if (_isPrimitive)    //track by index
            {
                var activeIndexes = force || Values == null
                    ? new int[] { }
                    : Values.Points.Select(x => x.Key).ToArray();

                var inactiveIndexes = Chart.ShapesMapper
                    .Where(m => Equals(m.Series, this) &&
                                !activeIndexes.Contains(m.ChartPoint.Key))
                    .ToArray();
                foreach (var s in inactiveIndexes)
                {
                    var p = s.Shape.Parent as Canvas;
                    if (p != null)
                    {
                        p.Children.Remove(s.HoverShape);
                        p.Children.Remove(s.Shape);
                        Chart.ShapesMapper.Remove(s);
                        Shapes.Remove(s.Shape);
                    }
                }
            }
            else                //track by instance reference
            {
                var activeInstances = force ? new object[] { } : Values.Points.Select(x => x.Instance).ToArray();
                var inactiveIntances = Chart.ShapesMapper
                    .Where(m => Equals(m.Series, this) &&
                                !activeInstances.Contains(m.ChartPoint.Instance))
                    .ToArray();

                foreach (var s in inactiveIntances)
                {
                    var p = s.Shape.Parent as Canvas;
                    if (p != null)
                    {
                        p.Children.Remove(s.HoverShape);
                        p.Children.Remove(s.Shape);
                        Chart.ShapesMapper.Remove(s);
                        Shapes.Remove(s.Shape);
                    }
                }
            }
        }

        private VisualHelper GetVisual(ChartPoint point)
        {
            var map = _isPrimitive
                ? Chart.ShapesMapper.FirstOrDefault(x => x.Series.Equals(this) &&
                                                         x.ChartPoint.Key == point.Key)
                : Chart.ShapesMapper.FirstOrDefault(x => x.Series.Equals(this) &&
                                                         x.ChartPoint.Instance == point.Instance);

            var pChart = Chart as PieChart;
            if (pChart == null)
                throw new InvalidCastException("Unexpected error converting chart to pie chart.");

            if (map == null)
            {
                var newSlice = new PieSlice
                {
                    Fill = Brushes != null && Brushes.Length > point.X
                        ? Brushes[(int) point.X]
                        : new SolidColorBrush(GetColorByIndex((int) point.X)),
                    Stroke = Stroke,
                    StrokeThickness = pChart.SlicePadding,
                    CentreX = 0,
                    CentreY = 0,
                    InnerRadius = pChart.InnerRadius
                };
                return new VisualHelper
                {
                    HoverShape = newSlice,
                    PointShape = newSlice,
                    IsNew = true
                };
            }

            return new VisualHelper
            {
                PointShape = (PieSlice) map.Shape,
                HoverShape = (PieSlice) map.HoverShape,
                IsNew = false
            };
        }

        private struct VisualHelper
        {
            public bool IsNew { get; set; }
            public PieSlice PointShape { get; set; }
            public PieSlice HoverShape { get; set; }
        }
    }
}
