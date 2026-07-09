using XpemFinancial.VMs;

namespace XpemFinancial.Utils
{
    /// <summary>
    /// Draws a two-series cumulative line chart (income / expense) using MAUI's
    /// built-in GraphicsView / IDrawable — no external libraries required.
    ///
    /// Layout
    /// ------
    ///  paddingLeft  |  plot area  |  paddingRight
    ///  paddingTop
    ///  [plot area — X: days, Y: cumulative value]
    ///  paddingBottom  (X axis labels)
    ///
    /// The caller is responsible for calling GraphicsView.Invalidate() whenever
    /// IncomePoints / ExpensePoints / XAxisPointCount / MaxValue change.
    /// </summary>
    public class LineChartDrawable : IDrawable
    {
        // ── data ──────────────────────────────────────────────────────────────
        public List<ChartPoint> IncomePoints { get; set; } = [];
        public List<ChartPoint> ExpensePoints { get; set; } = [];
        public int XAxisPointCount { get; set; } = 30;
        public string[]? XAxisLabels { get; set; }
        public decimal MaxValue { get; set; } = 1;

        // ── colours ───────────────────────────────────────────────────────────
        private static readonly Color IncomeColor = Color.FromArgb("#2bbf69");
        private static readonly Color ExpenseColor = Color.FromArgb("#f75c5c");
        private static readonly Color GridColor = Color.FromArgb("#2b3548");
        private static readonly Color AxisLabelColor = Color.FromArgb("#9da9b9");
        private static readonly Color BackgroundColor = Color.FromArgb("#191d24");

        // ── layout constants (in device-independent pixels) ───────────────────
        private const float PadLeft = 58f;
        private const float PadRight = 12f;
        private const float PadTop = 16f;
        private const float PadBottom = 32f;
        private const float LabelFontSize = 10f;
        private const int YGridLines = 5;

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

                // Grid line
                canvas.DrawLine(PadLeft, y, PadLeft + plotW, y);

                // Y-axis label (right-aligned before the plot)
                string label = FormatValue(labelVal);
                canvas.DrawString(label, 0, y - LabelFontSize / 2f, PadLeft - 4f, LabelFontSize + 2f,
                    HorizontalAlignment.Right, VerticalAlignment.Center);
            }

            // ── X axis labels ──────────────────────────────────────────────────
            if (XAxisLabels is null)
            {
                // Monthly mode: step-based numeric labels (existing logic)
                int xLabelStep = XAxisPointCount <= 15 ? 2 : XAxisPointCount <= 20 ? 4 : 5;
                for (int d = 1; d <= XAxisPointCount; d++)
                {
                    if (d == 1 || d % xLabelStep == 0 || d == XAxisPointCount)
                    {
                        float x = PointToX(d, plotW);
                        canvas.DrawString(d.ToString(), PadLeft + x - 10f, PadTop + plotH + 4f,
                            20f, LabelFontSize + 2f,
                            HorizontalAlignment.Center, VerticalAlignment.Top);
                    }
                }
            }
            else
            {
                // Annual mode (or custom labels): render each label at its X position
                for (int i = 0; i < XAxisLabels.Length; i++)
                {
                    float x = PointToX(i + 1, plotW);
                    canvas.DrawString(XAxisLabels[i], PadLeft + x - 14f, PadTop + plotH + 4f,
                        28f, LabelFontSize + 2f,
                        HorizontalAlignment.Center, VerticalAlignment.Top);
                }
            }

            // ── Series lines ──────────────────────────────────────────────────
            DrawSeries(canvas, IncomePoints, plotW, plotH, IncomeColor);
            DrawSeries(canvas, ExpensePoints, plotW, plotH, ExpenseColor);

            // ── Axes (drawn on top of grid) ───────────────────────────────────
            canvas.StrokeColor = AxisLabelColor;
            canvas.StrokeSize = 1.5f;
            // Y axis
            canvas.DrawLine(PadLeft, PadTop, PadLeft, PadTop + plotH);
            // X axis
            canvas.DrawLine(PadLeft, PadTop + plotH, PadLeft + plotW, PadTop + plotH);
        }

        private void DrawSeries(ICanvas canvas, List<ChartPoint> points, float plotW, float plotH, Color color)
        {
            if (points.Count == 0) return;

            canvas.StrokeColor = color;
            canvas.StrokeSize = 2f;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.StrokeLineJoin = LineJoin.Round;

            // Build path
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

            // Dots at each data point
            canvas.FillColor = color;
            foreach (var pt in points)
            {
                float x = PadLeft + PointToX(pt.Day, plotW);
                float y = PadTop + ValueToY((double)pt.Value, plotH);
                canvas.FillCircle(x, y, 3f);
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
