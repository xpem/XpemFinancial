---
inclusion: fileMatch
fileMatchPattern: "**/.github/workflows/*.yml"
---

# Gitea Actions - Servidor Local

Os projetos XpemFinancial e UniqueServer utilizam Gitea Actions rodando em um servidor local self-hosted, não GitHub Actions.

## Características do ambiente

- Runner self-hosted em Ubuntu
- Workloads .NET pré-instalados no servidor (ex: maui-android)
- Ao atualizar o .NET SDK no servidor, reinstalar os workloads necessários
