<div align="center">
  <img src="Organizer.Application/Assets/Icons/organizer.png" alt="Organizer" width="96" height="96" />

  <h1>Organizer</h1>

  <p>
    Um app desktop para organizar, buscar, visualizar e montar referências visuais com tags,
    grupos de imagens e um workspace livre para composição.
  </p>

  <p>
    <img alt=".NET" src="https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" />
    <img alt="Avalonia" src="https://img.shields.io/badge/Avalonia-12.0-7B5CFF?style=for-the-badge" />
    <img alt="SQLite" src="https://img.shields.io/badge/SQLite-EF%20Core-003B57?style=for-the-badge&logo=sqlite&logoColor=white" />
    <img alt="License" src="https://img.shields.io/badge/License-Apache%202.0-green?style=for-the-badge" />
  </p>
</div>

---

## Sobre

**Organizer** é um aplicativo desktop feito com **Avalonia**, **MVVM** e **SQLite** para manter uma biblioteca local de imagens. Ele permite registrar imagens individuais ou grupos, associar tags, pesquisar por descrição ou tag, visualizar previews, copiar imagens de grupos e usar um workspace tipo canvas para colar, mover, redimensionar e salvar composições visuais.

O projeto foi pensado para um fluxo rápido de referência visual: salvar imagens, classificar com tags, encontrar depois e montar painéis temporários de trabalho sem depender de serviços externos.

## Recursos

- Cadastro de imagens individuais ou grupos de imagens.
- Reordenação de imagens antes de salvar um grupo.
- Associação de tags a imagens.
- Busca por descrição e tags, com filtros e ordenação.
- Preview de imagens salvas.
- Edição da descrição e das tags da imagem de capa.
- Cópia seletiva de imagens dentro de grupos.
- Gerenciamento de tags com cores.
- Workspace livre com colagem via clipboard, seleção, movimentação, resize, zoom e pan.
- Undo/redo no workspace.
- Salvamento e abertura de workspaces em arquivo `.zip`.
- Autosave do workspace quando ele já possui arquivo associado.
- Preferências de tema, idioma, paginação, confirmação de exclusão, zoom inicial e comportamento de colagem.
- Interface em português do Brasil e inglês.

## Stack

| Camada | Tecnologia |
| --- | --- |
| Desktop UI | Avalonia 12 |
| Padrão de UI | MVVM |
| ViewModels | CommunityToolkit.Mvvm |
| Persistência | EF Core 10 + SQLite |
| Banco local | SQLite com WAL |
| Runtime | .NET 10 |

## Estrutura do Projeto

```text
Organizer
├── Program.cs
├── Organizer.csproj
├── Organizer.Application
│   ├── Views
│   ├── ViewModels
│   ├── Components
│   ├── Controls
│   ├── Services
│   └── Assets
├── Organizer.Core
│   ├── entity
│   ├── Enums
│   ├── Helpers
│   ├── Interfaces
│   └── Services
└── Organizer.Infrastructure
    └── Data
```

## Como Rodar

### Pré-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/)
- Linux, Windows ou macOS com suporte a aplicações desktop Avalonia

### Restaurar e compilar

```bash
dotnet build Organizer.csproj
```

### Executar

```bash
dotnet run --project Organizer.csproj
```

### Limpar build

```bash
dotnet clean Organizer.csproj
```

## Dados Locais

O Organizer salva os dados localmente em SQLite.

Em `Debug`, o banco fica junto da saída da aplicação:

```text
organizer-dev.db
```

Em builds fora de `Debug`, o banco e as preferências ficam na pasta de dados do usuário:

```text
Organizer/organizer.db
Organizer/settings.json
```

## Workspace

O workspace funciona como um canvas de referências:

- `Ctrl+V` cola imagens da área de transferência.
- `Ctrl+C` copia a imagem selecionada.
- `Delete` remove os itens selecionados.
- `Ctrl+Z` desfaz.
- `Ctrl+Y` ou `Ctrl+Shift+Z` refaz.
- Roda do mouse controla o zoom.
- Botão do meio do mouse move a câmera.

Os workspaces podem ser salvos como `.zip`. O arquivo contém um manifesto `workspace.json` e os assets usados no canvas.

## Desenvolvimento

O projeto segue uma organização simples por camadas:

- **Views e ViewModels** concentram a lógica de tela e interação.
- **Services** executam operações de aplicação e persistência.
- **Core** contém entidades, enums, helpers e contratos.
- **Infrastructure** configura o `AppDbContext`.

Atualmente não há um projeto de testes automatizados. Antes de enviar alterações, rode:

```bash
dotnet build Organizer.csproj
```

Para mudanças visuais ou de fluxo, valide manualmente cadastro, busca, preview, cópia, edição, tags, workspace e navegação.

## Licença

Distribuído sob a licença **Apache 2.0**. Veja [LICENSE](LICENSE) para mais detalhes.
