# 📋 Plano de Expansão: Arquitetura Multi-Contas (XpemFinancial)

Este documento estabelece o planejamento para a evolução do XpemFinancial de um modelo de conta única para um ecossistema **Multi-Contas (Offline-First)**. O objetivo é permitir que o usuário gerencie diferentes carteiras (Conta Corrente, Cartão Alimentação, Investimentos) sem perder a centralização dos relatórios de gastos.

---

## 🎯 1. Objetivos Estratégicos
* **Isolamento de Saldos:** Permitir que contas de benefícios (VR/VA) ou investimentos tenham saldos separados da conta corrente principal.
* **Consolidação de Despesas:** Garantir que, independentemente da conta utilizada para o pagamento (ex: dinheiro ou vale-alimentação), o gasto seja contabilizado nos relatórios e gráficos globais.
* **Preservação do Sync:** Adaptar a nova estrutura para manter a idempotência e o fluxo offline-first com a API remota.

---

## 🏗️ 2. Alterações na Camada de Modelos (`Model`)

Precisamos introduzir o conceito de tipo de conta e vincular as transações a uma conta específica.

### 2.1. Novo Enum: `AccountType`
Define o comportamento e a natureza do fluxo financeiro da conta.
```csharp
public enum AccountType
{
    Checking,    // Conta Corrente / Carteira Dinheiro
    Savings,     // Poupança / Plataformas de Investimento
    Benefits     // Cartão Alimentação, Refeição, Combustível, etc.
}
2.2. Nova Entidade: Account
C#
public class Account
{
    public Guid Id { get; set; }
    public string Name { get; set; } // Ex: "Nubank", "Sodexo Alimentação", "XP"
    public AccountType Type { get; set; }
    public decimal CurrentBalance { get; set; }
    
    // Flag crucial para o cálculo do Painel Principal
    public bool IncludeInGeneralBalance { get; set; } 
    
    // Controle de Sincronização
    public Guid? ExternalId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsActive { get; set; } // Soft-delete
}
2.3. Atualização na Entidade Transaction
A transação deixa de ser global e passa a pertencer obrigatoriamente a uma conta.

C#
public class Transaction
{
    // ... propriedades existentes (Id, Description, Value, Date, Type...)
    
    // Chave Estrangeira para a nova tabela de Contas
    public Guid AccountId { get; set; } 
}
🧠 3. Regras de Negócio e Casos de Uso (Services)
📌 Caso 1: O Cartão Alimentação (VR/VA)
Configuração: Conta criada como Type = AccountType.Benefits e IncludeInGeneralBalance = false.

Fluxo de Entrada: Carga mensal do benefício é lançada como Income (Receita) dentro desta conta. O saldo do cartão aumenta; o saldo geral do usuário permanece inalterado.

Fluxo de Saída: Compra no supermercado é lançada como Expense (Despesa), na categoria "Supermercado", vinculada à conta do cartão.

Impacto: O saldo do cartão diminui. No relatório mensal, o gasto aparece normalmente no gráfico do usuário.

📌 Caso 2: Conta de Investimentos
Configuração: Conta criada como Type = AccountType.Savings e IncludeInGeneralBalance = false (ou true, caso o usuário queira ver os investimentos somados ao seu patrimônio líquido total).

Rendimento: Juros e dividendos são lançados como Income (Receita) na categoria "Rendimentos", associada à conta de investimentos.

📉 4. Impacto nas Telas e UI (XpemFinancial)
4.1. Dashboard Principal (Visualização Mensal)
Filtro Global: Adicionar um seletor (Picker/Menu) no topo da tela.

Opção padrão: "Todas as Contas (Consolidado)"

Opções dinâmicas: Listar as contas ativas do usuário ("Nubank", "Sodexo", etc.).

Cálculo de Saldos (Lógica LINQ):

Se "Todas as Contas": O saldo exibido será a soma de CurrentBalance apenas das contas onde IncludeInGeneralBalance == true. As despesas/receitas exibidas listam todas as transações do mês de todas as contas.

Se uma Conta específica selecionada: Filtra estritamente o saldo e as transações pertencentes àquele AccountId.

4.2. Tela de Cadastro de Transações
Novo Campo: Adicionar um seletor de Conta (AccountId).

Preenchimento Automático: Ao carregar a tela, pré-selecionar a conta que estava ativa no Dashboard para melhorar a experiência do usuário.

⚡ 5. Roteiro de Implementação (Roadmap)
[ ] Fase 1: Banco de Dados Local (Repo)

Criar a migração/tabela de Accounts.

Atualizar a tabela Transactions para incluir AccountId.

Script de Migração: Garantir que, ao atualizar o app, todas as transações antigas sejam vinculadas a uma conta padrão automática (ex: "Conta Principal").

[ ] Fase 2: Lógica de Negócio (Services)

Atualizar o cálculo de saldo anterior e saldo mensal no TransactionService para respeitar o filtro de contas.

Implementar métodos de CRUD para AccountService.

[ ] Fase 3: Sincronização (ApiRepo / Sync)

Atualizar a API para receber a entidade Account.

Modificar a ordem de sincronização no Login: Categorias ➔ Contas ➔ Regras Recorrentes ➔ Transações (Contas agora vêm antes das transações devido à chave estrangeira).

[ ] Fase 4: Interface do Usuário (UI)

Criar tela simples de gerenciamento/cadastro de contas.

Adicionar o seletor de contas na tela de nova transação e no topo da Dashboard.