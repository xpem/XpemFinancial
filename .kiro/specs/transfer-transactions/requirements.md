# Requirements Document

## Introduction

Este documento especifica os requisitos para a funcionalidade de transferência entre contas no XpemFinancial. Uma transferência representa a movimentação interna de dinheiro entre duas contas do mesmo usuário. Diferentemente de receitas e despesas, transferências são neutras — não alteram o patrimônio total do usuário, apenas redistribuem saldo entre contas.

## Glossary

- **Sistema**: O aplicativo XpemFinancial (cliente MAUI + servidor UniqueServer)
- **Transferência**: Transação do tipo `Transfer` que representa movimentação de dinheiro de uma conta de origem para uma conta de destino
- **Conta_Origem**: Conta da qual o dinheiro é retirado durante uma transferência
- **Conta_Destino**: Conta para a qual o dinheiro é direcionado durante uma transferência
- **TransactionEditPage**: Tela de criação e edição de transações no aplicativo
- **MainPage**: Tela principal que exibe a lista de transações do mês
- **ChartPage**: Tela de gráficos que exibe o resumo financeiro visual
- **Totais**: Cálculo de saldo anterior, receitas, despesas e total do mês
- **Registro_Único**: Modelo em que uma transferência é representada por um único registro no banco de dados (não dois registros vinculados)

## Requirements

### Requisito 1: Modelo de dados da transferência

**User Story:** Como desenvolvedor, quero que a transferência seja representada como um registro único com conta de origem e conta de destino, para que a lógica de edição e exclusão seja simples e consistente.

#### Critérios de Aceitação

1. THE Sistema SHALL armazenar cada transferência como um Registro_Único no banco de dados com uma propriedade `AccountId` (Conta_Origem) e uma propriedade `DestinationAccountId` (Conta_Destino), onde ambas as propriedades referenciam contas existentes pertencentes ao mesmo usuário
2. THE Sistema SHALL definir o campo `Type` da transferência como `TransactionType.Transfer`
3. THE Sistema SHALL armazenar o valor da transferência (`Amount`) como o valor absoluto informado pelo usuário convertido para negativo (representando a saída da Conta_Origem), rejeitando valores iguais a zero
4. IF a Conta_Origem e a Conta_Destino informadas possuírem o mesmo identificador, THEN THE Sistema SHALL rejeitar a operação com uma mensagem de erro indicando que as contas devem ser distintas, sem persistir o registro
5. THE Sistema SHALL definir o campo `CategoryId` como nulo para transações do tipo Transfer

### Requisito 2: Criação de transferência na TransactionEditPage

**User Story:** Como usuário, quero criar uma transferência selecionando o tipo "Transferência" na tela de edição de transações, para que eu possa registrar movimentações entre minhas contas.

#### Critérios de Aceitação

1. WHEN o usuário seleciona o tipo "Transferência" na TransactionEditPage, THE Sistema SHALL exibir um campo de seleção de Conta_Destino contendo apenas as contas ativas do usuário, excluindo a conta atualmente selecionada como Conta_Origem
2. WHEN o usuário seleciona o tipo "Transferência" na TransactionEditPage, THE Sistema SHALL ocultar o seletor de categoria e as opções de parcelamento e recorrência
3. WHEN o usuário seleciona um tipo diferente de "Transferência" após ter selecionado "Transferência", THE Sistema SHALL ocultar o campo de Conta_Destino e restaurar a visibilidade do seletor de categoria e das opções de parcelamento e recorrência
4. WHEN o usuário confirma a criação de uma transferência com valor maior que zero, Conta_Origem selecionada e Conta_Destino selecionada, THE Sistema SHALL salvar o registro com a Conta_Origem e a Conta_Destino preenchidas
5. IF o usuário tenta salvar uma transferência sem selecionar a Conta_Destino, THEN THE Sistema SHALL exibir uma mensagem de validação solicitando a seleção da Conta_Destino e impedir o salvamento
6. IF o usuário seleciona a mesma conta como Conta_Origem e Conta_Destino, THEN THE Sistema SHALL exibir uma mensagem de validação indicando que as contas devem ser diferentes e impedir o salvamento

### Requisito 3: Edição e exclusão de transferência

**User Story:** Como usuário, quero editar ou excluir uma transferência existente, para que correções e cancelamentos afetem automaticamente ambas as contas envolvidas.

#### Critérios de Aceitação

1. WHEN o usuário edita o valor de uma transferência de V_antigo para V_novo, THE Sistema SHALL reverter V_antigo e aplicar V_novo nos saldos da Conta_Origem e da Conta_Destino (subtrair V_novo da Conta_Origem, adicionar V_novo à Conta_Destino)
2. WHEN o usuário altera a Conta_Origem ou a Conta_Destino de uma transferência, THE Sistema SHALL reverter o impacto da transferência nas contas anteriores e aplicar o impacto nas contas novas
3. WHEN o usuário exclui uma transferência de valor V, THE Sistema SHALL adicionar V ao saldo da Conta_Origem e subtrair V do saldo da Conta_Destino
4. WHEN o usuário abre uma transferência para edição, THE Sistema SHALL exibir a Conta_Destino previamente selecionada no campo correspondente
5. WHEN o usuário altera o tipo de uma transação de Transfer para outro tipo, THE Sistema SHALL reverter o impacto da transferência no saldo da Conta_Destino e limpar o campo DestinationAccountId

### Requisito 4: Impacto no saldo das contas

**User Story:** Como usuário, quero que ao criar uma transferência o saldo da conta de origem diminua e o saldo da conta de destino aumente pelo mesmo valor, para que meu patrimônio total permaneça inalterado.

#### Critérios de Aceitação

1. WHEN uma transferência de valor V é criada, THE Sistema SHALL subtrair V do saldo da Conta_Origem
2. WHEN uma transferência de valor V é criada, THE Sistema SHALL adicionar V ao saldo da Conta_Destino
3. THE Sistema SHALL manter o patrimônio total do usuário (soma dos saldos de todas as contas) inalterado após a criação, edição ou exclusão de uma transferência

### Requisito 5: Neutralidade em totais e gráficos

**User Story:** Como usuário, quero que transferências não distorçam meus relatórios financeiros, para que receitas e despesas reflitam apenas movimentações reais com o mundo externo.

#### Critérios de Aceitação

1. THE Sistema SHALL excluir transações do tipo Transfer do cálculo do campo Income nos Totais
2. THE Sistema SHALL excluir transações do tipo Transfer do cálculo do campo Expense nos Totais
3. THE Sistema SHALL excluir transações do tipo Transfer do cálculo das séries de pontos acumulados (IncomePoints e ExpensePoints) exibidas no gráfico da ChartPage
4. THE Sistema SHALL excluir transações do tipo Transfer da lista de transações exibida na ChartPage
5. WHILE o filtro por conta está ativo, THE Sistema SHALL exibir transferências na lista de transações da conta filtrada sem incluí-las nos campos Income ou Expense dos Totais nem nas séries de pontos acumulados do gráfico

### Requisito 6: Exibição na lista de transações (MainPage)

**User Story:** Como usuário, quero visualizar transferências na lista de transações do mês com diferenciação visual, para que eu identifique facilmente movimentações internas.

#### Critérios de Aceitação

1. WHEN uma transação do tipo Transfer pertence ao mês atualmente selecionado, THE Sistema SHALL exibi-la na lista de transações da MainPage na mesma ordem de classificação aplicada às demais transações (Income, Expense, Adjustment)
2. WHEN uma transação do tipo Transfer é renderizada na lista, THE Sistema SHALL exibir um ícone de seta bidirecional (ou equivalente) à esquerda ou à direita da descrição, com tamanho consistente com os demais indicadores visuais da lista
3. WHEN uma transação do tipo Transfer é renderizada na lista, THE Sistema SHALL exibir o valor (Amount) em uma cor neutra distinta tanto da cor de receita quanto da cor de despesa
4. WHEN uma transação do tipo Transfer é renderizada na lista, THE Sistema SHALL exibir o nome da conta destino como texto complementar na linha da transação, truncado com reticências caso exceda o espaço disponível
5. IF a transação do tipo Transfer não possuir conta destino associada, THEN THE Sistema SHALL exibir a linha da transferência sem o texto complementar de conta destino, mantendo os demais elementos visuais (ícone e cor neutra)

### Requisito 7: Sincronização com o servidor

**User Story:** Como usuário, quero que transferências sejam sincronizadas com o servidor seguindo o mesmo fluxo de push/pull das demais transações, para que meus dados estejam disponíveis em todos os dispositivos.

#### Critérios de Aceitação

1. WHEN uma transferência é criada ou atualizada localmente e a Conta_Destino possui ExternalId, THE Sistema SHALL incluir o campo `DestinationAccountId` (com o ExternalId da Conta_Destino) no payload `TransactionReq` enviado ao servidor
2. IF a Conta_Destino não possuir ExternalId no momento do push, THEN THE Sistema SHALL adiar o envio da transferência ao servidor marcando-a com SyncStatus Pending para tentativa no próximo ciclo de sincronização
3. WHEN o servidor retorna uma transação do tipo Transfer no pull e o `DestinationAccountId` corresponde a uma conta local, THE Sistema SHALL resolver o `DestinationAccountId` externo para o Id local da Conta_Destino e associá-lo à transação
4. IF durante o pull o `DestinationAccountId` retornado pelo servidor não corresponder a nenhuma conta local, THEN THE Sistema SHALL armazenar a transação sem Conta_Destino associada e marcá-la para reatribuição quando a conta for sincronizada
5. THE Sistema SHALL aplicar as mesmas regras de sincronização (SyncStatus, last-writer-wins, deduplicação por TransactionId) para transações do tipo Transfer

### Requisito 8: Cálculo de saldo anterior

**User Story:** Como usuário, quero que transferências sejam consideradas no cálculo do saldo anterior de cada conta individual, para que o saldo exibido reflita a realidade de cada conta.

#### Critérios de Aceitação

1. WHEN o saldo anterior de uma conta específica é calculado, THE Sistema SHALL somar o valor (Amount) de todas as transações do tipo Transfer associadas a essa conta (como origem ou destino) com data anterior ao primeiro dia do mês selecionado, juntamente com as demais transações (Income, Expense, Adjustment) da mesma conta
2. WHEN o saldo anterior geral (accountId nulo) é calculado, THE Sistema SHALL incluir todas as transações de Transfer no somatório, de modo que o par de lançamentos de cada transferência (saída na conta origem + entrada na conta destino) resulte em impacto líquido zero no total consolidado
3. IF uma transação do tipo Transfer estiver marcada como inativa (Inactive = true), THEN THE Sistema SHALL excluí-la do cálculo de saldo anterior, tanto no cálculo por conta quanto no cálculo geral
