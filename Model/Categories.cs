using System;
using System.Collections.Generic;
using System.Text;

namespace Model
{
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int? ParentId { get; set; }
        public bool IsCategory { get; set; }
    }

    public class Categories
    {
        public string Category { get; set; }
        public List<string>? Subcategories { get; set; }

        public class TransactionCategories
        {
            public int Id { get; set; } = 0;

            public string Category { get; set; }

            public List<string>? Subcategories { get; set; }

            public static List<TransactionCategories> LoadTransactionCategories()
            {
                return
            [
            new()
                {
                    Category = "Alimentação",
                    Subcategories = new List<string> { "Almoço", "Lanche" }
                },
                new()
                {
                    Category = "Carro",
                    Subcategories = new List<string> { "Combustível", "Estacionamento", "Lavagem", "Multas", "Pedágio" }
                   },
                   new(){
                        Category = "Casa",
                        Subcategories = new List<string> { "Aluguel", "Condomínio", "Internet", "Energia", "Manutenção", "Limpeza", "Móveis", "Utensílios" }
                   },
                new(){
                      Category = "Educação",
                      Subcategories = new List<string> { "Cursos", "Livros", "Pós-Graduação" }
                },
                new(){
                      Category = "Doações",
                      Subcategories = null
                },
                new(){
                      Category = "Eletrônicos",
                      Subcategories = null
                },
                new(){
                      Category = "Presentes",
                      Subcategories = null
                },
                new(){
                      Category = "Pessoais",
                      Subcategories = new List<string> { "Academia", "Assessório", "Celular", "Cosmético", "Roupa", "Calçado", "Servidor" }
                },
                new(){
                      Category = "Impostos",
                      Subcategories = new List<string> { "IR", "IPTU", "IPVA", "FGTS" }
                },
                new(){
                      Category = "Lazer",
                      Subcategories = new List<string> { "Streaming", "Bar", "Cinema", "Show", "Jogo", "Viagem" }
                },
                new(){
                      Category = "Outros",
                      Subcategories = null
                },
                new(){
                      Category = "Receita",
                      Subcategories = new List<string> { "13°", "Bonificação", "Comissão", "Estorno", "Férias", "Juros", "Reembolso", "Salário", "Outra" }
                },
                new(){
                      Category = "Saúde",
                      Subcategories = new List<string> { "Plano de Saúde", "Dentista", "Enxame", "Farmácia", "Médico" }
                },
                new(){
                      Category = "Seguro",
                      Subcategories = new List<string> { "Carro", "Moto", "Vida", "Residencial" }
                },
                new(){
                      Category = "Transporte",
                      Subcategories = new List<string> { "Combustível", "Estacionamento", "Lavagem", "Metrô", "Multas", "Pedágio", "Transporte por app" }
                },
                new(){
                      Category = "Investimentos",
                      Subcategories = new List<string> { "Ações", "Fundos Imobiliários", "Renda Fixa", "Criptomoedas" }
                },
               ];


                //categories = new List<string> {
                //    "Alimentação/Almoço",
                //    "Alimentação/Lanche",
                //    "Supermercado",
                //    "Carro",
                //    "Casa/Aluguel" ,
                //    "Casa/Condomínio",
                //    "Casa/Internet",
                //    "Casa/Energia",
                //    "Casa/Manutenção",
                //    "Casa/Limpeza",
                //    "Casa/Móveis",
                //    "Casa/Utensílios",
                //    "Educação/Cursos",
                //    "Educação/Livros",
                //    "Educação/Pós-Graduação",
                //    "Doações",
                //    "Eletrônicos",
                //    "Presentes",
                //    "Pessoais/Academia",
                //    "Pessoais/Assessório",
                //    "Pessoais/Celular",
                //    "Pessoais/Cosmético",
                //    "Pessoais/Roupa",
                //    "Pessoais/Calçado",
                //    //pessoal
                //    "Pessoais/Servidor",

                //    "Impostos/IR",
                //    "Impostos/IPTU",
                //    "Impostos/IPVA",
                //    "Impostos/FGTS",

                //    "Lazer/Streaming",
                //    "Lazer/Bar",
                //    "Lazer/Cinema",
                //    "Lazer/Show",
                //    "Lazer/Jogo",
                //    "Lazer/Viagem",

                //    "Outros",

                //    "Receita/13°",
                //    "Receita/Bonificação",
                //    "Receita/Comissão",
                //    "Receita/Estorno",
                //    "Receita/Férias",
                //    "Receita/Juros",
                //    "Receita/Reembolso",
                //    "Receita/Salário",
                //    "Receita/Outra",

                //    "Saúde/Plano de Saúde",
                //    "Saúde/Dentista",
                //    "Saúde/Enxame",
                //    "Saúde/Farmácia",
                //    "Saúde/Médico",

                //    "Seguro/Carro",
                //    "Seguro/Moto",
                //    "Seguro/Vida",
                //    "Seguro/Residencial",

                //    "Transporte/Combustível",
                //    "Transporte/Estacionamento",
                //    "Transporte/Lavagem",
                //    "Transporte/Metrô",
                //    "Transporte/Multas",
                //    "Transporte/Pedágio",
                //    "Transporte/Transporte por app",
                //};
            }
        }
    }
}
