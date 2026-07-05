# Implementation Plan: Transfer Transactions

## Overview

Implementação da funcionalidade de transferências entre contas no XpemFinancial. A abordagem segue a ordem: modelo de dados → repositório → serviço → UI (ViewModel + View) → sincronização → testes. Cada etapa constrói sobre a anterior, garantindo que não haja código órfão.

## Tasks

- [x] 1. Modelo de dados e infraestrutura
  - [x] 1.1 Adicionar propriedades de transferência ao TransactionDTO
    - Adicionar `DestinationAccountId` (int?), `DestinationAccountExternalId` (int?, NotMapped), e navigation property `DestinationAccount` (AccountDTO?, ForeignKey) ao `TransactionDTO`
    - Garantir que `TransactionType` enum já possui o valor `Transfer` (adicionar se necessário)
    - _Requirements: 1.1, 1.2_

  - [x] 1.2 Adicionar DestinationAccountId ao TransactionReq
    - Adicionar propriedade `DestinationAccountId` (int?) ao modelo `TransactionReq`
    - _Requirements: 7.1_

  - [x] 1.3 Adicionar DestinationAccountId ao TransactionApiRes
    - Adicionar propriedade `DestinationAccountId` (int?) ao modelo `TransactionApiRes`
    - _Requirements: 7.3_

  - [x] 1.4 Criar migration no UniqueServer para DestinationAccountId
    - Adicionar coluna `DestinationAccountId` (INT NULL) à tabela Transaction no SQL Server
    - Atualizar o modelo `TransactionDTO` do servidor com a nova propriedade
    - Atualizar `TransactionReq` e `TransactionRes` no servidor para incluir `DestinationAccountId`
    - _Requirements: 1.1, 7.1, 7.3_

- [x] 2. Repositório local (TransactionRepo)
  - [x] 2.1 Atualizar queries para incluir DestinationAccount
    - Adicionar `.Include(t => t.DestinationAccount)` nas queries relevantes (GetByIdAsync, GetByMonthYear)
    - _Requirements: 3.4, 6.4_

  - [x] 2.2 Atualizar GetPreviousBalanceAsync para considerar transferências como destino
    - Modificar a query de saldo anterior para incluir transações Transfer onde a conta é `DestinationAccountId` (contribuindo com `-Amount`, ou seja, valor positivo)
    - Quando accountId é nulo (geral), garantir que transferências são incluídas normalmente (net-zero por natureza)
    - Excluir transações com `Inactive == true`
    - _Requirements: 8.1, 8.2, 8.3_

- [x] 3. Camada de serviço (TransactionService)
  - [x] 3.1 Implementar lógica de criação de transferência em AddAsync
    - Se `Type == Transfer`: forçar `Amount = -Math.Abs(amount)`, `CategoryId = null`, `Repetition = None`
    - Validar: `DestinationAccountId != null`, `DestinationAccountId != AccountId`, `Amount != 0`
    - Recalcular saldo da `DestinationAccount` (adicionar `|Amount|`)
    - _Requirements: 1.3, 1.4, 1.5, 4.1, 4.2_

  - [x] 3.2 Implementar lógica de edição de transferência em UpdateAsync
    - Detectar mudança de valor: reverter impacto antigo e aplicar novo em ambas as contas
    - Detectar mudança de DestinationAccountId: reverter saldo na conta destino anterior, aplicar na nova
    - Se tipo mudou de Transfer para outro: limpar `DestinationAccountId`, reverter saldo da conta destino
    - Se tipo mudou de outro para Transfer: aplicar regras de criação de transferência
    - _Requirements: 3.1, 3.2, 3.5, 4.3_

  - [x] 3.3 Implementar lógica de exclusão de transferência em DeleteAsync
    - Ao soft-delete de uma transferência: reverter impacto no saldo da `DestinationAccount` (subtrair `|Amount|`)
    - _Requirements: 3.3, 4.3_

  - [x] 3.4 Atualizar cálculo de Totais para excluir transferências
    - Excluir transações com `Type == Transfer` do cálculo de `Income` e `Expense` nos Totais
    - Manter transferências visíveis na lista de transações da MainPage
    - Quando filtro por conta está ativo: exibir transferências na lista mas não incluí-las em Income/Expense
    - _Requirements: 5.1, 5.2, 5.5_

- [x] 4. Checkpoint - Verificar model, repo e service
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. ViewModel e UI (TransactionEditVM)
  - [x] 5.1 Adicionar propriedades de transferência ao TransactionEditVM
    - Adicionar `DestinationAccounts` (List<AccountDTO>), `SelectedDestinationAccount` (AccountDTO?), `IsTransfer` (bool) como ObservableProperty
    - Implementar filtragem: `DestinationAccounts` = contas ativas excluindo a conta origem selecionada
    - _Requirements: 2.1, 2.2_

  - [x] 5.2 Implementar lógica de toggle de tipo Transfer no TransactionEditVM
    - `OnSelectedTransactionTypeChanged`: se Transfer → `IsTransfer = true`, ocultar categoria/parcelamento/recorrência; senão → `IsTransfer = false`, restaurar visibilidade
    - `OnSelectedAccountChanged`: re-filtrar `DestinationAccounts` excluindo a nova conta origem
    - Na edição: carregar `SelectedDestinationAccount` a partir do registro existente
    - _Requirements: 2.2, 2.3, 3.4_

  - [x] 5.3 Implementar validação de transferência no TransactionEditVM
    - Impedir salvamento se `IsTransfer && SelectedDestinationAccount == null` (mensagem: "Selecione a conta de destino")
    - Impedir salvamento se `SelectedDestinationAccount == SelectedAccount` (mensagem: "As contas devem ser diferentes")
    - _Requirements: 2.5, 2.6_

  - [x] 5.4 Atualizar TransactionEditPage XAML para transferências
    - Adicionar Picker de conta destino (visível quando `IsTransfer == true`)
    - Ocultar seletor de categoria quando `IsTransfer == true`
    - Ocultar opções de parcelamento/recorrência quando `IsTransfer == true`
    - _Requirements: 2.1, 2.2, 2.3_

- [x] 6. Exibição na MainPage e ChartPage
  - [x] 6.1 Atualizar MainVM para exibição de transferências na lista
    - Transferências já aparecem na lista (não filtradas por tipo)
    - Garantir que transferências NÃO contribuem para Income/Expense nos totais exibidos
    - _Requirements: 5.1, 5.2, 6.1_

  - [x] 6.2 Atualizar template da lista de transações na MainPage
    - Exibir ícone de seta bidirecional para transações do tipo Transfer
    - Exibir valor em cor neutra (distinta de receita/despesa)
    - Exibir nome da conta destino como texto complementar (truncado com reticências se necessário)
    - Se não há conta destino associada: omitir texto complementar, manter ícone e cor neutra
    - _Requirements: 6.2, 6.3, 6.4, 6.5_

  - [x] 6.3 Atualizar ChartVM para excluir transferências
    - Excluir transações Transfer das séries `IncomePoints` e `ExpensePoints`
    - Excluir transações Transfer da lista de transações exibida na ChartPage
    - _Requirements: 5.3, 5.4_

- [x] 7. Sincronização com servidor
  - [x] 7.1 Atualizar PushAsync para incluir DestinationAccountId
    - No payload `TransactionReq`: incluir `DestinationAccountId` com o `ExternalId` da conta destino
    - Se conta destino não tem `ExternalId`: adiar push, marcar `SyncStatus = Pending`
    - _Requirements: 7.1, 7.2_

  - [x] 7.2 Atualizar PullAsync para resolver DestinationAccountId
    - Resolver `DestinationAccountId` externo para Id local da conta destino
    - Se conta destino não existe localmente: armazenar transação sem destino, marcar para reatribuição quando conta sincronizar
    - Aplicar mesmas regras de sincronização (SyncStatus, last-writer-wins, deduplicação)
    - _Requirements: 7.3, 7.4, 7.5_

- [x] 8. Checkpoint - Verificar funcionalidade completa
  - Ensure all tests pass, ask the user if questions arise.

- [x] 9. Testes de propriedade (FsCheck)
  - [x] 9.1 Configurar projeto de testes e generators para Transfer
    - Criar generators FsCheck customizados para `TransactionDTO` com `Type == Transfer`
    - Criar generators para listas de contas válidas (IDs distintos, saldos arbitrários)
    - Configurar mocks de repositórios para testes de lógica pura
    - _Requirements: 1.1_

  - [x] 9.2 Write property test: Transfer field invariants
    - **Property 1: Transfer field invariants**
    - Verificar que para qualquer criação com amount != 0 e contas distintas: Amount < 0, CategoryId == null, DestinationAccountId != null, DestinationAccountId != AccountId
    - **Validates: Requirements 1.3, 1.4, 1.5**

  - [x] 9.3 Write property test: Dual balance impact on creation
    - **Property 3: Dual balance impact on transfer creation**
    - Verificar que para qualquer transferência de valor V > 0: saldo origem diminui V, saldo destino aumenta V
    - **Validates: Requirements 4.1, 4.2**

  - [x] 9.4 Write property test: Transfer is zero-sum
    - **Property 4: Transfer is zero-sum (patrimony invariant)**
    - Verificar que para qualquer operação de transferência (criação, edição, exclusão): soma total dos saldos permanece inalterada
    - **Validates: Requirements 4.3**

  - [x] 9.5 Write property test: Balance correction on value edit
    - **Property 5: Balance correction on transfer value edit**
    - Verificar que para edição de V_old para V_new: variação no saldo origem = (V_old - V_new), variação no saldo destino = (V_new - V_old)
    - **Validates: Requirements 3.1, 3.2**

  - [x] 9.6 Write property test: Balance reversal on deletion
    - **Property 6: Balance reversal on transfer deletion**
    - Verificar que para exclusão: saldo origem aumenta V, saldo destino diminui V
    - **Validates: Requirements 3.3**

  - [x] 9.7 Write property test: Transfer neutrality in totals
    - **Property 7: Transfer neutrality in Income/Expense totals**
    - Verificar que para qualquer coleção de transações incluindo transfers: Income soma apenas tipo Income, Expense soma apenas tipo Expense
    - **Validates: Requirements 5.1, 5.2, 5.5**

  - [x] 9.8 Write property test: Transfer exclusion from chart series
    - **Property 8: Transfer exclusion from chart series**
    - Verificar que séries IncomePoints/ExpensePoints agregam apenas transações do tipo correspondente
    - **Validates: Requirements 5.3**

  - [x] 9.9 Write property test: Per-account previous balance includes transfers
    - **Property 9: Per-account previous balance includes transfers as origin and destination**
    - Verificar que saldo anterior por conta inclui Amount de transfers onde conta é origem E -Amount onde conta é destino
    - **Validates: Requirements 8.1**

  - [x] 9.10 Write property test: General previous balance net-zero
    - **Property 10: General previous balance net-zero for transfers**
    - Verificar que no cálculo geral (sem filtro de conta), contribuição líquida de todas as transferências é zero
    - **Validates: Requirements 8.2**

- [x] 10. Testes unitários
  - [x] 10.1 Write unit tests para TransactionService (criação, edição, exclusão)
    - Testar criação com campos válidos (happy path)
    - Testar validação: conta destino não selecionada → exceção
    - Testar validação: mesma conta origem e destino → exceção
    - Testar validação: valor zero → exceção
    - Testar edição com mudança de contas
    - Testar exclusão reverte saldos
    - _Requirements: 1.3, 1.4, 1.5, 3.1, 3.2, 3.3_

  - [x] 10.2 Write unit tests para TransactionEditVM (UI logic)
    - Testar: tipo Transfer oculta categoria e parcelamento
    - Testar: mudança de tipo restaura visibilidade
    - Testar: edição carrega conta destino corretamente
    - Testar: filtro de contas destino exclui conta origem
    - _Requirements: 2.1, 2.2, 2.3, 3.4_

  - [x] 10.3 Write unit tests para sincronização
    - Testar: push com conta destino sem ExternalId adia envio
    - Testar: pull com conta destino desconhecida armazena sem destino
    - Testar: pull resolve ExternalId para Id local
    - _Requirements: 7.1, 7.2, 7.3, 7.4_

  - [x] 10.4 Write unit tests para Totais e ChartVM
    - Testar: transferência não aparece em Income/Expense dos Totais
    - Testar: transferência excluída da lista e séries da ChartPage
    - Testar: saldo anterior por conta inclui transferências como destino
    - Testar: transferência sem conta destino não exibe texto complementar
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 6.5, 8.1_

- [x] 11. Final checkpoint - Todos os testes passam
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties using FsCheck + xUnit
- Unit tests validate specific examples and edge cases
- O projeto de testes pode ser adicionado ao `RecurringTests` existente ou a um novo projeto `TransferTests`
- A migration no servidor (task 1.4) pode ser feita via EF Core `Add-Migration` no projeto UniqueServer

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3", "1.4"] },
    { "id": 1, "tasks": ["2.1", "2.2"] },
    { "id": 2, "tasks": ["3.1", "3.2", "3.3", "3.4"] },
    { "id": 3, "tasks": ["5.1", "5.2", "5.3", "5.4", "6.1", "6.2", "6.3"] },
    { "id": 4, "tasks": ["7.1", "7.2"] },
    { "id": 5, "tasks": ["9.1"] },
    { "id": 6, "tasks": ["9.2", "9.3", "9.4", "9.5", "9.6", "9.7", "9.8", "9.9", "9.10"] },
    { "id": 7, "tasks": ["10.1", "10.2", "10.3", "10.4"] }
  ]
}
```
