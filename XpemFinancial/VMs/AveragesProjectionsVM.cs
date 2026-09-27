using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Model.DTO;
using Service.Transaction;
using System.Globalization;

namespace XpemFinancial.VMs
{
    /// <summary>
    /// Médias de entrada/saída dos últimos 6 meses (incluindo o mês atual) e projeção
    /// dos próximos 3 meses, aplicando a própria média de forma constante (linha achatada).
    /// </summary>
    public partial class AveragesProjectionsVM(ITransactionService transactionService) : VMBase
    {
        private static readonly string[] MonthAbbreviations =
            ["Jan", "Fev", "Mar", "Abr", "Mai", "Jun", "Jul", "Ago", "Set", "Out", "Nov", "Dez"];

        private const int HistoryMonths = 6;
        private const int ProjectionMonths = 3;

        /// <summary>
        /// Um mês histórico só é marcado como "fora do padrão" se desviar da mediana em mais
        /// de 50% — evita destacar flutuações normais como se fossem um outlier.
        /// </summary>
        private const decimal OutlierDeviationRatio = 0.5m;

        private static readonly CultureInfo PtBr = new("pt-BR");

        [ObservableProperty] private decimal averageIncome;
        [ObservableProperty] private decimal averageExpense;
        [ObservableProperty] private decimal averageBalance;
        [ObservableProperty] private string subtitleText = $"Últimos {HistoryMonths} meses + projeção de {ProjectionMonths} meses.";

        [ObservableProperty] private bool isScenarioActive;
        [ObservableProperty] private bool isScenarioExpense = true;
        [ObservableProperty] private string scenarioDeltaText = "0,00";
        [ObservableProperty] private string scenarioSummary = string.Empty;

        /// <summary>Linha simulada (ponto de transição + 3 meses projetados), null quando o cenário está inativo.</summary>
        public List<ChartPoint>? SimulatedProjectionPoints { get; private set; }

        /// <summary>Pontos de entrada: 6 reais (histórico) + 3 projetados.</summary>
        public List<ChartPoint> IncomePoints { get; private set; } = [];

        /// <summary>Pontos de saída: 6 reais (histórico) + 3 projetados.</summary>
        public List<ChartPoint> ExpensePoints { get; private set; } = [];

        /// <summary>Rótulos do eixo X ("abr/25" .. "dez/25"), 9 no total.</summary>
        public string[] XAxisLabels { get; private set; } = [];

        public int XAxisPointCount => HistoryMonths + ProjectionMonths;

        /// <summary>Índice a partir do qual os pontos passam a ser projeção (usado pelo drawable).</summary>
        public int RealPointCount => HistoryMonths;

        public decimal MaxValue { get; private set; } = 1;

        /// <summary>Dia (1..6) do mês de entrada mais distante da mediana, se ultrapassar o desvio mínimo; senão null.</summary>
        public int? IncomeOutlierIndex { get; private set; }

        /// <summary>Dia (1..6) do mês de saída mais distante da mediana, se ultrapassar o desvio mínimo; senão null.</summary>
        public int? ExpenseOutlierIndex { get; private set; }

        /// <summary>Raised quando os dados mudam, para o GraphicsView se invalidar.</summary>
        public event Action? DataChanged;

        partial void OnIsScenarioActiveChanged(bool value) => RecomputeScenario();
        partial void OnIsScenarioExpenseChanged(bool value) => RecomputeScenario();
        partial void OnScenarioDeltaTextChanged(string value) => RecomputeScenario();

        [RelayCommand]
        private void ToggleScenario() => IsScenarioActive = !IsScenarioActive;

        [RelayCommand]
        private void SetScenarioTarget(bool isExpense) => IsScenarioExpense = isExpense;

        public async Task InitializeAsync() => await LoadAsync();

        private async Task LoadAsync()
        {
            IsBusy = true;

            try
            {
                var today = DateTime.Today;
                var from = new DateTime(today.Year, today.Month, 1).AddMonths(-(HistoryMonths - 1));
                var to = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));

                var transactions = (await transactionService.GetByDateRangeAsync(from, to)).ToList();

                var months = Enumerable.Range(0, HistoryMonths)
                    .Select(i => from.AddMonths(i))
                    .ToList();

                var incomeByMonth = transactions
                    .Where(t => t.Type == TransactionType.Income)
                    .GroupBy(t => (t.Date.Year, t.Date.Month))
                    .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));

                var expenseByMonth = transactions
                    .Where(t => t.Type == TransactionType.Expense)
                    .GroupBy(t => (t.Date.Year, t.Date.Month))
                    .ToDictionary(g => g.Key, g => g.Sum(t => Math.Abs(t.Amount)));

                var incomeTotals = months
                    .Select(m => incomeByMonth.TryGetValue((m.Year, m.Month), out var v) ? v : 0)
                    .ToList();

                var expenseTotals = months
                    .Select(m => expenseByMonth.TryGetValue((m.Year, m.Month), out var v) ? v : 0)
                    .ToList();

                // Meses sem NENHUMA transação (de qualquer tipo) são tratados como "ainda sem
                // dados" (ex.: antes do usuário começar a usar o app) e ficam de fora do cálculo
                // da mediana — diferente de um mês real com entrada ou saída genuinamente zero.
                var monthsWithData = transactions
                    .Select(t => (t.Date.Year, t.Date.Month))
                    .ToHashSet();

                var realMonthIndices = Enumerable.Range(0, HistoryMonths)
                    .Where(i => monthsWithData.Contains((months[i].Year, months[i].Month)))
                    .ToList();

                var incomeTotalsForAverage = realMonthIndices.Select(i => incomeTotals[i]).ToList();
                var expenseTotalsForAverage = realMonthIndices.Select(i => expenseTotals[i]).ToList();

                AverageIncome = Median(incomeTotalsForAverage);
                AverageExpense = Median(expenseTotalsForAverage);
                AverageBalance = AverageIncome - AverageExpense;

                int? incomeOutlierPosition = FindOutlierIndex(incomeTotalsForAverage, AverageIncome);
                int? expenseOutlierPosition = FindOutlierIndex(expenseTotalsForAverage, AverageExpense);
                IncomeOutlierIndex = incomeOutlierPosition.HasValue ? realMonthIndices[incomeOutlierPosition.Value - 1] + 1 : null;
                ExpenseOutlierIndex = expenseOutlierPosition.HasValue ? realMonthIndices[expenseOutlierPosition.Value - 1] + 1 : null;

                SubtitleText = realMonthIndices.Count switch
                {
                    0 => "Sem dados suficientes para calcular médias e projeção.",
                    var n when n < HistoryMonths =>
                        $"Baseado em {n} {(n == 1 ? "mês" : "meses")} com dados (histórico ainda curto) + projeção de {ProjectionMonths} meses.",
                    _ => $"Últimos {HistoryMonths} meses + projeção de {ProjectionMonths} meses.",
                };

                var incomePoints = new List<ChartPoint>();
                var expensePoints = new List<ChartPoint>();

                for (int i = 0; i < HistoryMonths; i++)
                {
                    incomePoints.Add(new ChartPoint(i + 1, incomeTotals[i]));
                    expensePoints.Add(new ChartPoint(i + 1, expenseTotals[i]));
                }

                for (int i = 0; i < ProjectionMonths; i++)
                {
                    incomePoints.Add(new ChartPoint(HistoryMonths + i + 1, AverageIncome));
                    expensePoints.Add(new ChartPoint(HistoryMonths + i + 1, AverageExpense));
                }

                IncomePoints = incomePoints;
                ExpensePoints = expensePoints;

                var futureMonths = Enumerable.Range(1, ProjectionMonths).Select(i => today.AddMonths(i));
                XAxisLabels = months.Concat(futureMonths)
                    .Select(m => $"{MonthAbbreviations[m.Month - 1]}/{m.Year % 100:00}")
                    .ToArray();

                var allValues = incomePoints.Select(p => p.Value).Concat(expensePoints.Select(p => p.Value));
                MaxValue = allValues.Any() ? Math.Max(allValues.Max(), 1) : 1;

                RecomputeScenario();
                DataChanged?.Invoke();
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Recalcula a linha simulada e a frase-resumo a partir de <see cref="AverageIncome"/>/
        /// <see cref="AverageExpense"/> já carregados — puro, sem I/O, para reagir instantaneamente
        /// enquanto o usuário digita/alterna o cenário.
        /// </summary>
        private void RecomputeScenario()
        {
            if (!IsScenarioActive || !decimal.TryParse(ScenarioDeltaText, NumberStyles.Currency, PtBr, out decimal delta))
            {
                SimulatedProjectionPoints = null;
                ScenarioSummary = string.Empty;
                DataChanged?.Invoke();
                return;
            }

            decimal baseline = IsScenarioExpense ? AverageExpense : AverageIncome;
            decimal simulatedValue = baseline + delta;

            var points = new List<ChartPoint> { new(RealPointCount, baseline) };
            for (int i = 1; i <= ProjectionMonths; i++)
                points.Add(new ChartPoint(RealPointCount + i, simulatedValue));

            SimulatedProjectionPoints = points;

            decimal simulatedBalance = IsScenarioExpense
                ? AverageBalance - delta
                : AverageBalance + delta;

            string label = IsScenarioExpense ? "saída" : "entrada";
            ScenarioSummary =
                $"Com esse cenário, sua {label} média passaria de {baseline.ToString("C", PtBr)} para {simulatedValue.ToString("C", PtBr)} — " +
                $"o saldo médio projetado iria de {AverageBalance.ToString("C", PtBr)} para {simulatedBalance.ToString("C", PtBr)}.";

            DataChanged?.Invoke();
        }

        /// <summary>
        /// Mediana de uma lista de valores — resistente a um único mês fora da curva
        /// (ex.: um bônus ou uma compra grande), diferente da média aritmética.
        /// </summary>
        private static decimal Median(List<decimal> values)
        {
            if (values.Count == 0) return 0m;

            var sorted = values.OrderBy(v => v).ToList();
            int mid = sorted.Count / 2;
            return sorted.Count % 2 == 0
                ? (sorted[mid - 1] + sorted[mid]) / 2m
                : sorted[mid];
        }

        /// <summary>
        /// Encontra o mês (1-based) mais distante da mediana, desde que o desvio ultrapasse
        /// <see cref="OutlierDeviationRatio"/>. Retorna null quando a mediana é zero (sem base
        /// de comparação estável) ou quando nenhum mês se destaca o suficiente.
        /// </summary>
        private static int? FindOutlierIndex(List<decimal> monthlyTotals, decimal median)
        {
            if (median == 0) return null;

            int worstIndex = -1;
            decimal worstRatio = 0;

            for (int i = 0; i < monthlyTotals.Count; i++)
            {
                decimal ratio = Math.Abs(monthlyTotals[i] - median) / median;
                if (ratio > worstRatio)
                {
                    worstRatio = ratio;
                    worstIndex = i;
                }
            }

            return worstRatio > OutlierDeviationRatio ? worstIndex + 1 : null;
        }
    }
}
