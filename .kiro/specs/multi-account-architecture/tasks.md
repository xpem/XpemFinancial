# Implementation Plan: Arquitetura Multi-Contas

## Overview

Implementação da evolução de conta única para multi-contas no XpemFinancial (MAUI) e UniqueServer (API). As tasks seguem a ordem de dependência: back-end primeiro (modelo → repo → service → endpoints), depois front-end (modelo → repo → service → api repo → sync → UI).

## Tasks

- [x] 1. Back-end: Expandir AccountDTO + Criar AccountType enum + Gerar EF Core Migration
  - [x] 1.1. Descomentar/adicionar Name (StringLength 100, required), Type (AccountType: Checking=0, Savings=1, Benefits=2), CurrentBalance (decimal), IncludeInGeneralBalance (bool, default true), Inactive (bool)
  - [x] 1.2. Tornar UserId non-nullable
  - [x] 1.3. Gerar migration com defaults (Name="Conta Principal", Type=0) e aplicar migration
- [x] 2. Back-end: Criar AccountReq + Expandir AccountRes
  - [x] 2.1. Criar `FinancialService/Model/Req/AccountReq.cs` (record, herda BaseReq): Name (Required, StringLength 100 min 1), Type, IncludeInGeneralBalance, Inactive, UpdatedAt
  - [x] 2.2. Expandir `AccountRes.cs`: adicionar Name, Type, CurrentBalance, IncludeInGeneralBalance
- [x] 3. Back-end: Expandir AccountRepo para multi-conta
  - [x] 3.1. Substituir GetAsync(uid) por GetByIdAsync(id, uid), GetAllAsync(uid), GetUpdatedAfterAsync(uid, updatedAt) retornando List
  - [x] 3.2. Implementar com Where + ToListAsync
- [x] 4. Back-end: Expandir AccountService + Registrar novos endpoints
  - [x] 4.1. Implementar CreateAsync (validação, persist, retorna AccountRes), UpdateAsync (validar ownership 403, atualizar campos), GetUpdatedAfterAsync (retorna lista), RecalculateBalanceAsync (SUM transações)
  - [x] 4.2. Registrar endpoints: GET /financial/accounts, POST /financial/account, PUT /financial/account/{id}. Validações 400/403/404
- [x] 5. Front-end: Expandir AccountDTO + Criar AccountType enum + Bump DB version
  - [x] 5.1. Adicionar Name, Type, CurrentBalance, IncludeInGeneralBalance, IsActive ao AccountDTO
  - [x] 5.2. Criar enum AccountType
  - [x] 5.3. Incrementar CurrentDbVersion para 14 em BuildDbService.cs
- [x] 6. Front-end: Expandir AccountRepo para multi-conta
  - [x] 6.1. Nova interface: GetAllAsync, GetActiveAsync, GetByIdAsync, GetByExternalIdAsync, GetDefaultAsync (OrderBy CreatedAt first), GetActiveCountAsync, GetPendingPushAsync (ExternalId null OR UpdatedAt > cursor), GetLocalIdByExternalIdAsync
  - [x] 6.2. Implementar com EF Core queries
- [x] 7. Front-end: Reescrever AccountService multi-conta
  - [x] 7.1. CRUD: CreateAsync com validação 50 limite, UpdateAsync, DeactivateAsync com validação última ativa
  - [x] 7.2. Saldo: RecalculateBalanceAsync, GetGeneralBalanceAsync
  - [x] 7.3. Migração: EnsureDefaultAccountAsync — cria "Conta Principal" se vazio, assign orphan transactions
  - [x] 7.4. AdjustAccountBalanceAsync (Type=Adjustment)
  - [x] 7.5. Registrar no DI
- [x] 8. Front-end: Expandir AccountApiRepo + AccountApiRes
  - [x] 8.1. Expandir AccountApiRes com Name, Type, CurrentBalance, IncludeInGeneralBalance
  - [x] 8.2. Criar AccountReq local para push
  - [x] 8.3. Adicionar GetAccountsAsync (GET plural, retorna List), PostAccountAsync (POST, retorna AccountApiRes), PutAccountAsync (PUT por externalId)
- [x] 9. Front-end: Atualizar fluxo de sincronização
  - [x] 9.1. Inserir Account push/pull entre Category e RecurringRule na orquestração
  - [x] 9.2. Implementar PullAsync (GET accounts, upsert por ExternalId, avançar cursor) e PushAsync (pendentes → POST/PUT, atualizar ExternalId)
  - [x] 9.3. Chamar EnsureDefaultAccountAsync após pull
  - [x] 9.4. Interromper sync de dependentes se Account falhar
- [x] 10. Front-end: TransactionService account-aware
  - [x] 10.1. Adicionar AssignAccountToOrphansAsync no TransactionRepo
  - [x] 10.2. Filtrar listagens por accountId opcional
  - [x] 10.3. Recalcular saldo da conta após criar/editar/desativar transação
  - [x] 10.4. Mapear AccountId → Account.ExternalId no push de transações (adiar se ExternalId null)
- [x] 11. Front-end: UI — Tela de Gerenciamento de Contas
  - [x] 11.1. Criar AccountsPage + AccountsViewModel (lista agrupada ativas/inativas)
  - [x] 11.2. Criar AccountFormPage + AccountFormViewModel (Name Entry, Type Picker, IncludeInGeneralBalance Switch)
  - [x] 11.3. Registrar no Shell e DI
  - [x] 11.4. Validações visuais e confirmação de desativação
- [x] 12. Front-end: UI — Dashboard filtro por conta
  - [x] 12.1. Adicionar Picker (Todas as Contas + contas ativas)
  - [x] 12.2. SelectedAccount no ViewModel
  - [x] 12.3. Filtrar transações do mês por AccountId
  - [x] 12.4. Saldo exibido: GeneralBalance se consolidado, CurrentBalance se conta específica
  - [x] 12.5. Preservar seleção na sessão
  - [x] 12.6. Reverter para consolidado se conta desativada
- [x] 13. Front-end: UI — Seletor de conta na criação de transação
  - [x] 13.1. Adicionar Picker de contas ativas na tela de transação
  - [x] 13.2. Pré-selecionar baseado no filtro do dashboard (parâmetro de navegação)
  - [x] 13.3. Fallback para GetDefaultAsync
  - [x] 13.4. Validação obrigatória
  - [x] 13.5. Recalcular saldo após save

## Task Dependency Graph

```json
{
  "waves": [
    [1, 5],
    [2, 6],
    [3, 7],
    [4],
    [8],
    [9],
    [10],
    [11],
    [12],
    [13]
  ]
}
```

## Notes

- O back-end (Tasks 1-4) pode ser desenvolvido em paralelo com o front-end model/repo (Tasks 5-6), mas as Tasks 8-9 do front-end dependem da API estar pronta.
- A Task 5 inclui o bump de versão do banco (14), o que causa drop+recreate do SQLite local. Dados são re-sincronizados do servidor no próximo login.
- Tasks de UI (11-13) podem ser parcialmente paralelas entre si, mas a 13 depende do filtro do dashboard (12) para a pré-seleção funcionar.
- O endpoint GET singular existente (`/financial/account`) pode ser mantido temporariamente como deprecated enquanto o front-end migra para o plural (`/financial/accounts`).
