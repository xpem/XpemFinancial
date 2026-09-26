using XpemFinancial.VMs;

namespace XpemFinancial.Utils
{
    /// <summary>
    /// Draws a two-series line chart (income / expense) where the first
    /// <see cref="RealPointCount"/> points are historical (solid line) and the
    /// remaining points are a flat projection (dashed line, reduced opacity),
    /// with a vertical marker at the boundary between the two.
    ///
    /// Shares layout/colour constants with <see cref="LineChartDrawable"/> to keep
    /// the same visual language as the Gráfico screen.
    /// </summary>
    public class AveragesChartDrawable : IDrawable
    {
        // ── data ──────────────────────────────────────────────────────────────
        public List<ChartPoint> IncomePoints { get; set; } = [];
        public List<ChartPoint> ExpensePoints { get; set; } = [];
        public int XAxisPointCount { get; set; } = 9;
        public string[]? XAxisLabels { get; set; }
        public decimal MaxValue { get; set; } = 1;

        /// <summary>Number of leading points that are real (historical) data; the rest are projection.</summary>
        public int RealPointCount { get; set; } = 6;

        /// <summary>Day (1-based) of the historical income point that deviates most from the median, if any.</summary>
        public int? IncomeOutlierIndex { get; set; }

        /// <summary>Day (1-based) of the historical expense point that deviates most from the median, if any.</summary>
        public int? ExpenseOutlierIndex { get; set; }

        // ── colours ───────────────────────────────────────────────────────────
        private static readonly Color IncomeColor = Color.FromArgb("#2bbf69");
        private static readonly Color ExpenseColor = Color.FromArgb("#f75c5c");
        private static readonly Color GridColor = Color.FromArgb("#2b3548");
        private static readonly Color AxisLabelColor = Color.FromArgb("#9da9b9");
        private static readonly Color BackgroundColor = Color.FromArgb("#191d24");
        private static readonly Color OutlierRingColor = Color.FromArgb("#ffcc00");

        // ── layout constants (in device-independent pixels) ───────────────────
        private const float PadLeft = 58f;
        private const float PadRight = 12f;
        private const float PadTop = 16f;
        private const float PadBottom = 32f;
        private const float LabelFontSize = 10f;
        private const int YGridLines = 5;
        private const float ProjectionOpacity = 0.6f;

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            float w = dirtyRect.Width;
            float h = dirtyRect.Height;

            float plotW = w - PadLeft - PadRight;
            float plotH = h - PadTop - PadBottom;

            if (plotW <= 0 || plotH <= 0) return;

            // Background
            canvas.FillColor = BackgroundColor;
            canvas.FillRectangle(dirtyRect);

            // ── Y grid lines & labels ──────────────────────────────────────────
            canvas.FontSize = LabelFontSize;
            canvas.FontColor = AxisLabelColor;
            canvas.StrokeColor = GridColor;
            canvas.StrokeSize = 1f;

            double maxDouble = (double)MaxValue;

            for (int i = 0; i <= YGridLines; i++)
            {
                float ratio = i / (float)YGridLines;
                float y = PadTop + plotH - ratio * plotH;
                double labelVal = maxDouble * ratio;

                canvas.DrawLine(PadLeft, y, PadLeft + plotW, y);

                string label = FormatValue(labelVal);
                canvas.DrawString(label, 0, y - LabelFontSize / 2f, PadLeft - 4f, LabelFontSize + 2f,
                    HorizontalAlignment.Right, VerticalAlignment.Center);
            }

            // ── X axis labels ──────────────────────────────────────────────────
            if (XAxisLabels is not null)
            {
                for (int i = 0; i < XAxisLabels.Length; i++)
                {
                    float x = PointToX(i + 1, plotW);
                    canvas.DrawString(XAxisLabels[i], PadLeft + x - 18f, PadTop + plotH + 4f,
                        36f, LabelFontSize + 2f,
                        HorizontalAlignment.Center, VerticalAlignment.Top);
                }
            }

            // ── "Hoje" boundary marker between the last real point and the first projected one ──
            if (RealPointCount >= 1 && RealPointCount < XAxisPointCount)
            {
                float boundaryX = PadLeft + (PointToX(RealPointCount, plotW) + PointToX(RealPointCount + 1, plotW)) / 2f;
                canvas.StrokeColor = AxisLabelColor;
                canvas.StrokeSize = 1f;
                canvas.StrokeDashPattern = [3, 3];
                canvas.DrawLine(boundaryX, PadTop, boundaryX, PadTop + plotH);
                canvas.StrokeDashPattern = null;
            }

            // ── Median reference lines (flat height already carried by the projected points) ──
            DrawReferenceLine(canvas, IncomePoints, plotW, plotH, IncomeColor);
            DrawReferenceLine(canvas, ExpensePoints, plotW, plotH, ExpenseColor);

            // ── Series lines ──────────────────────────────────────────────────
            DrawSeries(canvas, IncomePoints, plotW, plotH, IncomeColor, IncomeOutlierIndex);
            DrawSeries(canvas, ExpensePoints, plotW, plotH, ExpenseColor, ExpenseOutlierIndex);

            // ── Axes (drawn on top of grid) ───────────────────────────────────
            canvas.StrokeColor = AxisLabelColor;
            canvas.StrokeSize = 1.5f;
            canvas.DrawLine(PadLeft, PadTop, PadLeft, PadTop + plotH);
            canvas.DrawLine(PadLeft, PadTop + plotH, PadLeft + plotW, PadTop + plotH);
        }

        /// <summary>
        /// Thin dotted line spanning the whole plot width at the series' median height — the
        /// same height already used for the flat projection — so the real, jagged history can
        /// be visually compared against the "typical" baseline.
        /// </summary>
        private void DrawReferenceLine(ICanvas canvas, List<ChartPoint> points, float plotW, float plotH, Color color)
        {
            if (points.Count == 0) return;

            decimal medianValue = points[^1].Value;
            float y = PadTop + ValueToY((double)medianValue, plotH);

            canvas.StrokeColor = color.WithAlpha(0.35f);
            canvas.StrokeSize = 1f;
            canvas.StrokeDashPattern = [2, 4];
            canvas.DrawLine(PadLeft, y, PadLeft + plotW, y);
            canvas.StrokeDashPattern = null;

            // Label the exact value this line represents (the median actually used for the projection),
            // so it's readable directly on the chart, not just in the stat tile above it.
            canvas.FontSize = LabelFontSize;
            canvas.FontColor = color;
            canvas.DrawString(FormatValue((double)medianValue), PadLeft + 4f, y - LabelFontSize - 4f, 60f, LabelFontSize + 2f,
                HorizontalAlignment.Left, VerticalAlignment.Bottom);
        }

        private void DrawSeries(ICanvas canvas, List<ChartPoint> points, float plotW, float plotH, Color color, int? outlierIndex)
        {
            if (points.Count == 0) return;

            int realCount = Math.Clamp(RealPointCount, 0, points.Count);

            // Solid segment: points[0..realCount)
            if (realCount > 0)
            {
                DrawSegment(canvas, points.Take(realCount).ToList(), plotW, plotH, color, dashed: false, outlierIndex);
            }

            // Dashed segment: from the last real point (transition) through the projected points.
            if (realCount < points.Count)
            {
                var dashedPoints = points.Skip(Math.Max(realCount - 1, 0)).ToList();
                DrawSegment(canvas, dashedPoints, plotW, plotH, color, dashed: true, outlierIndex: null);
            }
        }

        private void DrawSegment(ICanvas canvas, List<ChartPoint> points, float plotW, float plotH, Color color, bool dashed, int? outlierIndex)
        {
            if (points.Count == 0) return;

            canvas.StrokeColor = dashed ? color.WithAlpha(ProjectionOpacity) : color;
            canvas.StrokeSize = 2f;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.StrokeLineJoin = LineJoin.Round;
            canvas.StrokeDashPattern = dashed ? [4, 4] : null;

            var path = new PathF();
            bool first = true;

            foreach (var pt in points)
            {
                float x = PadLeft + PointToX(pt.Day, plotW);
                float y = PadTop + ValueToY((double)pt.Value, plotH);

                if (first) { path.MoveTo(x, y); first = false; }
                else path.LineTo(x, y);
            }

            canvas.DrawPath(path);
            canvas.StrokeDashPattern = null;

            // Markers: filled dots for real points, hollow dots for projected points.
            foreach (var pt in points)
            {
                float x = PadLeft + PointToX(pt.Day, plotW);
                float y = PadTop + ValueToY((double)pt.Value, plotH);

                if (dashed)
                {
                    canvas.StrokeColor = color.WithAlpha(ProjectionOpacity);
                    canvas.StrokeSize = 1.5f;
                    canvas.DrawCircle(x, y, 3f);
                }
                else
                {
                    canvas.FillColor = color;
                    canvas.FillCircle(x, y, 3f);

                    if (outlierIndex.HasValue && pt.Day == outlierIndex.Value)
                    {
                        canvas.StrokeColor = OutlierRingColor;
                        canvas.StrokeSize = 2f;
                        canvas.DrawCircle(x, y, 6f);
                    }
                }
            }
        }

        // Maps an index (1..XAxisPointCount) → X offset inside the plot area
        private float PointToX(int index, float plotW)
            => (index - 1) / (float)(XAxisPointCount - 1 == 0 ? 1 : XAxisPointCount - 1) * plotW;

        // Maps a value (0..MaxValue) → Y offset inside the plot area (Y increases downward)
        private float ValueToY(double value, float plotH)
            => plotH - (float)(value / (double)MaxValue) * plotH;

        private static string FormatValue(double v)
        {
            if (v >= 1_000_000) return $"{v / 1_000_000:0.#}M";
            if (v >= 1_000) return $"{v / 1_000:0.#}k";
            return $"{v:0}";
        }
    }
}
