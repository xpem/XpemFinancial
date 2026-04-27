using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Graphics;

namespace XpemFinancial.VMs
{
    public partial class TransactionEditVM : ObservableObject
    {
        private string transactionTypeColor;

        public string TransactionTypeColor
        {
            get => transactionTypeColor;
            set
            {
                if (transactionTypeColor != value)
                {
                    SetProperty(ref transactionTypeColor, value);
                }
            }
        }

        [ObservableProperty]
        private DateTime transactionDate;

        [ObservableProperty]
        private string description;

        [ObservableProperty]
        private string selectedCategory;

        [ObservableProperty]
        private List<string> categories;

        public TransactionEditVM()
        {
            transactionTypeColor = "#f75c5c";//Color.FromArgb("#2bbf69"); // Cor padrão para transações de entrada
            transactionDate = DateTime.Now;
            //passar isso pro banco de dados depois
            categories = new List<string> {
                "Alimentação/Almoço",
                "Alimentação/Lanche",
                "Supermercado",
                "Carro",
                "Casa/Aluguel" ,
                "Casa/Condomínio",
                "Casa/Internet",
                "Casa/Energia",
                "Casa/Manutenção",
                "Casa/Limpeza",
                "Casa/Móveis",
                "Casa/Utensílios",
                "Educação/Cursos",
                "Educação/Livros",
                "Educação/Pós-Graduação",
                "Doações",
                "Eletrônicos",
                "Presentes",
                "Pessoais/Academia",
                "Pessoais/Assessório",
                "Pessoais/Celular",
                "Pessoais/Cosmético",
                "Pessoais/Roupa",
                "Pessoais/Calçado",
                //pessoal
                "Pessoais/Servidor",

                "Impostos/IR",
                "Impostos/IPTU",
                "Impostos/IPVA",
                "Impostos/FGTS",

                "Lazer/Streaming",
                "Lazer/Bar",
                "Lazer/Cinema",
                "Lazer/Show",
                "Lazer/Jogo",
                "Lazer/Viagem",

                "Outros",

                "Receita/13°",
                "Receita/Bonificação",
                "Receita/Comissão",
                "Receita/Estorno",
                "Receita/Férias",
                "Receita/Juros",
                "Receita/Reembolso",
                "Receita/Salário",
                "Receita/Outra",

                "Saúde/Plano de Saúde",
                "Saúde/Dentista",
                "Saúde/Enxame",
                "Saúde/Farmácia",
                "Saúde/Médico",

                "Seguro/Carro",
                "Seguro/Moto",
                "Seguro/Vida",
                "Seguro/Residencial",

                "Transporte/Combustível",
                "Transporte/Estacionamento",
                "Transporte/Lavagem",
                "Transporte/Metrô",
                "Transporte/Multas",
                "Transporte/Pedágio",
                "Transporte/Transporte por app",
            };
        }
    }
}
