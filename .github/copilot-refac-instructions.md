# Skill: Refatoração de Código C#

Aplique os princípios abaixo ao revisar ou refatorar o código informado.

## Regras

1. Reduza indentação para no máximo 2 níveis. Use cláusulas de guarda (`if (condição) return;`).
2. Substitua `foreach` de filtragem/busca por LINQ (`.Any()`, `.Where()`, `.Select()`).
3. Extraia lógicas repetidas em métodos de extensão ou serviços utilitários.
4. Utilize recursos do C# 10+ (Pattern Matching, Collection Expressions, Primary Constructors).
5. Não sugira padrões complexos (Factory, Strategy, Decorator) para operações simples.
6. Prefira `record` e `init` para DTOs.
7. Use nomes claros e descritivos para variáveis e métodos.
8. Forneça o conteúdo completo do arquivo refatorado (não saída parcial).
9. Se o código estiver complexo demais, diga explicitamente e mostre a versão simplificada.
