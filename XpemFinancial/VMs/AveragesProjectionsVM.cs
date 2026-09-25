using CommunityToolkit.Mvvm.ComponentModel;
using Model.DTO;
using Service.Transaction;

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

        [ObservableProperty] private decimal averageIncome;
        [ObservableProperty] private decimal averageExpense;
        [ObservableProperty] private decimal averageBalance;

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

        /// <summary>Raised quando os dados mudam, para o GraphicsView se invalidar.</summary>
        public event Action? DataChanged;

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

                AverageIncome = incomeTotals.Average();
                AverageExpense = expenseTotals.Average();
                AverageBalance = AverageIncome - AverageExpense;

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

                DataChanged?.Invoke();
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
