# Requirements Document

## Introduction

Evolução do XpemFinancial de um modelo de conta única para uma arquitetura multi-contas. O objetivo é permitir ao usuário gerenciar diferentes contas financeiras (Conta Corrente, Poupança/Investimentos, Benefícios como VA/VR/Combustível) com saldos isolados, mantendo a consolidação de despesas em relatórios globais e preservando o fluxo offline-first de sincronização com a API remota (UniqueServer).

## Glossary

- **Sistema_App**: Aplicativo MAUI (XpemFinancial) executado localmente no dispositivo do usuário.
- **Sistema_API**: Servidor back-end (UniqueServer/FinancialService) que armazena dados remotos e processa sincronização.
- **Conta**: Entidade representando uma carteira financeira do usuário (ex: "Nubank", "Sodexo Alimentação").
- **TipoConta**: Enumeração que classifica a conta como Checking (Conta Corrente), Savings (Poupança/Investimentos) ou Benefits (Benefícios VA/VR/Combustível).
- **Transação**: Registro financeiro de receita, despesa, transferência ou ajuste vinculado a uma Conta.
- **SaldoGeral**: Soma dos saldos das contas marcadas com IncludeInGeneralBalance = true.
- **ContaPadrão**: Conta criada automaticamente na migração para vincular transações existentes.
- **SyncCursor**: Marcador temporal usado para sincronização incremental entre Sistema_App e Sistema_API.
- **Dashboard**: Tela principal do aplicativo que exibe resumo financeiro mensal.

## Requirements

### Requisito 1: Entidade Conta no Sistema_App

**User Story:** Como usuário, eu quero criar e gerenciar múltiplas contas financeiras, para que eu possa organizar meus saldos separadamente por carteira.

#### Critérios de Aceitação

1. THE Sistema_App SHALL persistir a entidade Conta com os campos: Id (int, chave primária local), Name (string, obrigatório, entre 1 e 100 caracteres sem considerar espaços em branco nas extremidades), Type (TipoConta — enum com valores: Checking, Savings, Benefits), CurrentBalance (decimal, intervalo de -999.999.999,99 a 999.999.999,99, valor padrão 0), IncludeInGeneralBalance (bool, valor padrão true), ExternalId (int?, referência ao servidor), UpdatedAt (DateTime), IsActive (bool), UserId (int).
2. WHEN o usuário criar uma nova Conta, THE Sistema_App SHALL atribuir IsActive = true, IncludeInGeneralBalance = true, CurrentBalance = 0 e UpdatedAt = data/hora atual (UTC).
3. WHEN o usuário desativar uma Conta, THE Sistema_App SHALL definir IsActive = false e atualizar UpdatedAt com a data/hora atual (UTC) sem remover o registro do banco de dados.
4. IF o usuário submeter uma Conta com Name vazio, composto apenas por espaços em branco, ou com mais de 100 caracteres após remoção de espaços nas extremidades, THEN THE Sistema_App SHALL rejeitar a operação e exibir mensagem de erro indicando a restrição de tamanho do nome.
5. WHEN o usuário editar Name, Type ou IncludeInGeneralBalance de uma Conta, THE Sistema_App SHALL atualizar o campo UpdatedAt com a data/hora atual (UTC).
6. THE Sistema_App SHALL permitir no máximo 50 contas ativas por usuário.
7. IF o usuário tentar criar uma Conta quando já existem 50 contas ativas para o mesmo UserId, THEN THE Sistema_App SHALL rejeitar a criação e exibir mensagem de erro indicando que o limite de contas foi atingido.

### Requisito 2: Entidade Conta no Sistema_API

**User Story:** Como desenvolvedor, eu quero que o back-end suporte CRUD de contas, para que a sincronização multi-contas funcione entre dispositivos.

#### Critérios de Aceitação

1. THE Sistema_API SHALL persistir a entidade Conta com os campos: Id (int, chave primária), Name (string, obrigatório, máximo 100 caracteres), Type (TipoConta), CurrentBalance (decimal, intervalo de -999999999.99 a 999999999.99), IncludeInGeneralBalance (bool), UserId (int), CreatedAt (DateTime), UpdatedAt (DateTime), Inactive (bool).
2. WHEN o Sistema_App enviar uma requisição POST para /financial/account com Name, Type e IncludeInGeneralBalance válidos, THE Sistema_API SHALL criar a Conta associada ao UserId autenticado, definir CreatedAt e UpdatedAt com a data/hora atual do servidor, e retornar o objeto com Id gerado.
3. WHEN o Sistema_App enviar uma requisição PUT para /financial/account/{id} com campos atualizáveis (Name, Type, IncludeInGeneralBalance, Inactive), THE Sistema_API SHALL atualizar somente esses campos na Conta correspondente ao Id e ao UserId autenticado e definir UpdatedAt com a data/hora atual do servidor.
4. WHEN o Sistema_App enviar uma requisição GET para /financial/account?updatedAt={timestamp}, THE Sistema_API SHALL retornar a lista de contas do usuário autenticado com UpdatedAt posterior ao timestamp fornecido, incluindo os campos Id, Name, Type, CurrentBalance, IncludeInGeneralBalance, Inactive e UpdatedAt.
5. IF uma requisição tentar acessar uma Conta que não pertence ao UserId autenticado, THEN THE Sistema_API SHALL retornar erro 403.
6. IF uma requisição POST ou PUT contiver Name vazio, Name com mais de 100 caracteres ou Type com valor fora da enumeração TipoConta, THEN THE Sistema_API SHALL rejeitar a requisição com erro 400 e uma mensagem indicando o campo inválido.
7. IF uma requisição PUT referenciar um Id de Conta inexistente para o UserId autenticado, THEN THE Sistema_API SHALL retornar erro 404.

### Requisito 3: Vinculação de Transação à Conta

**User Story:** Como usuário, eu quero que cada transação esteja vinculada a uma conta específica, para que meus saldos por carteira sejam calculados corretamente.

#### Critérios de Aceitação

1. THE Sistema_App SHALL exigir que toda Transação possua um AccountId válido referenciando uma Conta existente (ativa ou inativa) no banco local.
2. WHEN o usuário criar uma Transação, THE Sistema_App SHALL persistir o AccountId selecionado na Transação.
3. WHEN o Sistema_App sincronizar uma Transação com o Sistema_API, THE Sistema_App SHALL enviar o AccountExternalId correspondente ao AccountId local; IF a Conta local possuir ExternalId nulo, THEN THE Sistema_App SHALL adiar o push da Transação até que a Conta tenha sido sincronizada e possua um ExternalId válido.
4. WHEN o Sistema_App receber uma Transação do Sistema_API, THE Sistema_App SHALL resolver o AccountId local a partir do ExternalId da Conta recebido no campo AccountId da transação remota.
5. IF o AccountId referenciado não existir localmente durante a resolução, THEN THE Sistema_App SHALL registrar um erro de sincronização com o ExternalId da Conta ausente, pular a transação sem interromper o fluxo, e tentar resolver novamente na próxima sincronização.

### Requisito 4: Migração de Dados Existentes

**User Story:** Como usuário existente, eu quero que minhas transações anteriores continuem funcionando após a atualização, para que eu não perca dados históricos.

#### Critérios de Aceitação

1. WHEN o Sistema_App detectar que não existe nenhuma Conta no banco local durante a inicialização, THE Sistema_App SHALL criar uma ContaPadrão com Name = "Conta Principal", Type = Checking, IncludeInGeneralBalance = true, IsActive = true e UserId igual ao do usuário autenticado localmente.
2. WHEN a ContaPadrão for criada, THE Sistema_App SHALL atribuir o AccountId da ContaPadrão a todas as transações existentes cujo AccountId seja nulo, em no máximo 30 segundos para até 50.000 transações.
3. THE Sistema_App SHALL executar a migração de forma idempotente: se já existir pelo menos uma Conta no banco local, nenhuma ContaPadrão adicional será criada e nenhum AccountId já preenchido será sobrescrito.
4. WHEN a migração local for concluída com sucesso, THE Sistema_App SHALL enviar a ContaPadrão ao Sistema_API na próxima sincronização seguindo a ordem definida no Requisito 9, e o Sistema_API SHALL persistir a Conta e retornar o Id gerado sem rejeitar as transações migradas vinculadas a ela.
5. IF a criação da ContaPadrão ou a atualização de AccountId nas transações falhar durante a migração, THEN THE Sistema_App SHALL reverter todas as alterações da migração (rollback) e tentar novamente na próxima inicialização, preservando os dados originais intactos.
6. WHEN o BuildDbService incrementar a versão do banco local, THE Sistema_App SHALL migrar o schema preservando os dados existentes em vez de deletar e recriar o banco, garantindo que transações e demais registros permaneçam íntegros após a atualização.

### Requisito 5: Cálculo de Saldo por Conta

**User Story:** Como usuário, eu quero visualizar o saldo individual de cada conta, para que eu saiba quanto tenho disponível em cada carteira.

#### Critérios de Aceitação

1. THE Sistema_App SHALL calcular o CurrentBalance de uma Conta como a soma dos Amount de todas as Transações vinculadas àquela Conta onde Inactive = false.
2. WHEN uma Transação for adicionada, editada, desativada ou sincronizada (pull), THE Sistema_App SHALL recalcular o CurrentBalance da(s) Conta(s) afetada(s).
3. WHEN uma Transação tiver seu AccountId alterado de ContaA para ContaB, THE Sistema_App SHALL recalcular o CurrentBalance de ambas as contas (ContaA e ContaB).
4. THE Sistema_App SHALL calcular o SaldoGeral como a soma de CurrentBalance das contas onde IncludeInGeneralBalance = true e IsActive = true.
5. WHEN a flag IncludeInGeneralBalance de uma Conta for alterada, THE Sistema_App SHALL recalcular o SaldoGeral antes de atualizar a exibição no Dashboard.

### Requisito 6: Dashboard com Filtro por Conta

**User Story:** Como usuário, eu quero filtrar o dashboard por conta, para que eu possa analisar receitas e despesas de uma carteira específica ou do consolidado.

#### Critérios de Aceitação

1. THE Dashboard SHALL exibir um seletor de contas com as opções: "Todas as Contas (Consolidado)" e cada Conta ativa do usuário, ordenadas por Name em ordem alfabética.
2. WHEN o filtro "Todas as Contas (Consolidado)" estiver selecionado, THE Dashboard SHALL exibir o SaldoGeral e listar todas as transações do mês corrente de todas as contas ativas, calculando receitas e despesas a partir dessas transações.
3. WHEN uma Conta específica estiver selecionada no filtro, THE Dashboard SHALL exibir apenas o CurrentBalance daquela Conta e as transações do mês pertencentes àquela Conta, calculando receitas e despesas somente a partir dessas transações.
4. WHEN o usuário navegar entre meses no Dashboard, THE Dashboard SHALL manter a Conta selecionada no filtro e aplicar o filtro ao novo mês exibido.
5. THE Dashboard SHALL preservar a seleção do filtro durante a navegação entre telas dentro da mesma sessão (desde a abertura até o encerramento completo do aplicativo).
6. WHEN o aplicativo for iniciado (cold start), THE Dashboard SHALL iniciar com o filtro "Todas as Contas (Consolidado)" selecionado.
7. IF a Conta atualmente selecionada no filtro for desativada, THEN THE Dashboard SHALL reverter o filtro para "Todas as Contas (Consolidado)" e atualizar os dados exibidos.

### Requisito 7: Seletor de Conta na Criação de Transação

**User Story:** Como usuário, eu quero selecionar a conta ao criar uma transação, para que o valor seja debitado ou creditado na carteira correta.

#### Critérios de Aceitação

1. THE tela de criação de Transação SHALL exibir um seletor listando todas as Contas ativas do usuário, apresentando o Name de cada Conta como texto de exibição.
2. WHEN o usuário navegar do Dashboard com uma Conta específica selecionada no filtro, THE tela de criação de Transação SHALL pré-selecionar essa Conta no seletor.
3. WHEN o filtro do Dashboard estiver em "Todas as Contas (Consolidado)", THE tela de criação de Transação SHALL pré-selecionar a ContaPadrão no seletor; IF a ContaPadrão estiver inativa, THEN THE tela de criação de Transação SHALL pré-selecionar a primeira Conta ativa da lista.
4. IF o usuário tentar salvar uma Transação sem Conta selecionada, THEN THE Sistema_App SHALL manter o usuário na tela de criação e exibir uma mensagem de validação indicando que a seleção de Conta é obrigatória.
5. WHEN o usuário alterar a Conta selecionada no seletor, THE tela de criação de Transação SHALL atualizar a Conta associada à Transação antes do salvamento sem limpar os demais campos preenchidos.

### Requisito 8: Tela de Gerenciamento de Contas

**User Story:** Como usuário, eu quero uma tela para criar, editar e desativar contas, para que eu possa organizar minhas carteiras conforme necessário.

#### Critérios de Aceitação

1. THE Sistema_App SHALL disponibilizar uma tela listando todas as Contas do usuário, agrupando contas ativas antes das inativas.
2. WHEN o usuário acessar a tela de gerenciamento, THE Sistema_App SHALL exibir para cada Conta: Name, Type, CurrentBalance e status ativo/inativo.
3. WHEN o usuário criar ou editar uma Conta, THE Sistema_App SHALL apresentar um formulário com os campos Name (texto, obrigatório, máximo 100 caracteres), Type (seletor de TipoConta) e IncludeInGeneralBalance (toggle booleano), e validar que Name não está vazio e não excede 100 caracteres antes de persistir.
4. IF a validação do formulário de Conta falhar, THEN THE Sistema_App SHALL exibir uma mensagem de erro indicando o campo inválido e manter os dados preenchidos no formulário.
5. WHEN o usuário solicitar a desativação de uma Conta, THE Sistema_App SHALL exibir uma confirmação antes de executar a desativação.
6. WHEN o usuário confirmar a desativação de uma Conta que possui transações vinculadas, THE Sistema_App SHALL manter as transações históricas visíveis nos relatórios filtrados por período.
7. IF o usuário tentar desativar a última Conta ativa, THEN THE Sistema_App SHALL exibir uma mensagem de erro informando que pelo menos uma conta ativa deve existir e não executar a desativação.

### Requisito 9: Ordem de Sincronização

**User Story:** Como desenvolvedor, eu quero que a sincronização respeite a dependência entre entidades, para que chaves estrangeiras sejam resolvidas corretamente.

#### Critérios de Aceitação

1. WHEN o Sistema_App iniciar o processo de sincronização (pull), THE Sistema_App SHALL executar na ordem: Categories, Accounts, RecurringRules, Transactions.
2. WHEN o Sistema_App enviar dados ao servidor (push), THE Sistema_App SHALL enviar na ordem: Categories, Accounts, RecurringRules, Transactions.
3. IF a sincronização de Categories falhar, THEN THE Sistema_App SHALL interromper a sincronização de Accounts, RecurringRules e Transactions, preservar os dados locais inalterados e registrar o erro com o nome da entidade que falhou.
4. IF a sincronização de Accounts falhar, THEN THE Sistema_App SHALL interromper a sincronização de RecurringRules e Transactions, preservar os dados locais inalterados e registrar o erro com o nome da entidade que falhou.
5. IF a sincronização de RecurringRules falhar, THEN THE Sistema_App SHALL interromper a sincronização de Transactions, preservar os dados locais inalterados e registrar o erro com o nome da entidade que falhou.
6. IF o Sistema_API estiver indisponível durante push de uma entidade, THEN THE Sistema_App SHALL manter os dados locais pendentes e sincronizá-los na próxima execução do ciclo de sincronização.

### Requisito 10: Sincronização Offline-First de Contas

**User Story:** Como usuário, eu quero que operações em contas funcionem offline e sincronizem depois, para que eu possa usar o app sem internet.

#### Critérios de Aceitação

1. THE Sistema_App SHALL persistir criação e edição de Contas localmente independente de conectividade com o Sistema_API.
2. WHEN a conectividade for restabelecida, THE Sistema_App SHALL enviar ao Sistema_API todas as Contas com ExternalId nulo ou com UpdatedAt posterior ao último SyncCursor de contas.
3. WHEN o Sistema_API retornar o Id gerado para uma Conta criada, THE Sistema_App SHALL atualizar o ExternalId local com o valor recebido.
4. WHEN o Sistema_App receber uma Conta do Sistema_API com UpdatedAt mais recente que a versão local, THE Sistema_App SHALL aplicar a versão do servidor como fonte de verdade e atualizar o registro local.
5. THE Sistema_App SHALL usar um SyncCursor dedicado para a entidade Conta (chave "Account"), independente dos cursores de Transação e Categoria.
6. WHEN a sincronização de Contas completar com sucesso, THE Sistema_App SHALL avançar o SyncCursor para o maior UpdatedAt recebido na resposta.
7. IF a sincronização de uma Conta individual falhar durante o push, THEN THE Sistema_App SHALL manter a Conta com ExternalId nulo e tentar novamente na próxima sincronização sem interromper o push das demais Contas pendentes.

### Requisito 11: Ajuste de Saldo por Conta

**User Story:** Como usuário, eu quero ajustar o saldo de uma conta específica, para que eu possa corrigir divergências sem afetar outras carteiras.

#### Critérios de Aceitação

1. WHEN o usuário solicitar ajuste de saldo informando o novo saldo desejado, THE Sistema_App SHALL criar uma Transação com Type = Adjustment, AccountId = Conta selecionada, Amount = novoSaldo - saldoAtual, Date = data/hora atual, e Description = "Ajuste de saldo".
2. IF o usuário tentar ajustar o saldo de uma Conta com IsActive = false, THEN THE Sistema_App SHALL exibir uma mensagem de erro indicando que ajustes só são permitidos em contas ativas e não criar a Transação.
3. WHEN um ajuste de saldo for realizado online, THE Sistema_App SHALL enviar a requisição ao Sistema_API e atualizar ExternalId da Transação de ajuste com o Id retornado.
4. WHEN um ajuste de saldo for realizado offline, THE Sistema_App SHALL persistir a Transação de ajuste localmente e sincronizar quando a conectividade for restabelecida conforme o fluxo de sincronização offline-first.
5. IF o novoSaldo informado for igual ao CurrentBalance atual da Conta, THEN THE Sistema_App SHALL ignorar a solicitação sem criar Transação de ajuste.
