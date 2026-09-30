# Technolife RustDesk Configurator

Ferramenta multiplataforma para instalar, detectar e configurar automaticamente o RustDesk dos clientes da Technolife para uso com a infraestrutura de acesso remoto da empresa.

## Objetivo

O projeto terá uma única base de código e distribuições específicas para cada sistema operacional suportado.

Fluxo esperado:

1. Detectar o sistema operacional.
2. Verificar se o RustDesk já está instalado.
3. Caso não esteja, instalar uma versão homologada.
4. Localizar o executável do RustDesk.
5. Aplicar a configuração do servidor Technolife.
6. Iniciar ou reiniciar o RustDesk quando necessário.
7. Validar a execução.
8. Registrar logs do procedimento.
9. Informar ao usuário se a configuração foi concluída ou se existe alguma ação manual pendente.

A configuração deverá priorizar os mecanismos suportados oficialmente pelo RustDesk, especialmente a importação por linha de comando:

```text
rustdesk --config "<CONFIG_STRING>"
```

> No Windows, o executável pode ser `rustdesk.exe`. Caminhos e argumentos deverão ser tratados pela camada específica de cada plataforma.

## Plataformas planejadas

| Plataforma | Arquitetura | Status |
|---|---|---|
| Windows 10/11 | x64 | RD-007.1 concluída localmente; Beta 2 ainda não publicada |
| Linux Debian/Ubuntu | x64 | Planejado |
| macOS | Intel x64 | Planejado |
| macOS | Apple Silicon arm64 | Planejado |
| Outras distribuições Linux | A definir | Futuro |

O projeto será desenvolvido como **um produto, um repositório e uma base de código**, com builds específicos por sistema operacional.

## Downloads

A `v0.1.0-beta.1` foi publicada para validação técnica, mas **não deve ser entregue a clientes finais**. A RD-007.1 corrigiu localmente o fluxo para usuário leigo; a nova experiência ainda aguarda versionamento e publicação em uma futura Beta 2.

| Sistema | Versão | Status | Download |
|---|---|---|---|
| Windows 10/11 x64 | `v0.1.0-beta.1` | ⛔ Referência técnica — não usar com cliente final | [Release anterior](https://github.com/jotaCorsino/technolife-rustdesk/releases/tag/v0.1.0-beta.1) |
| Windows 10/11 x64 | `v0.1.0-beta.2` | 🟡 RD-007.1 concluída localmente | Ainda não publicada |
| Linux x64 | — | ⚪ Planejado | — |
| macOS Intel | — | ⚪ Planejado | — |
| macOS Apple Silicon | — | ⚪ Planejado | — |

O requisito de uso final no Windows é: **baixar o EXE → dar duplo clique → acompanhar a configuração em uma janela simples → concluir**, sem exigir PowerShell, parâmetros ou conhecimento técnico.

## Infraestrutura RustDesk da Technolife

Configuração pública atualmente prevista:

```text
ID Server: remoto.technolife.net.br
Relay Server: remoto.technolife.net.br
Public Key: mFwxUtIrUDZkAI3zvh4RrvJIMlipFye+ILMbwtdK8jM=
```

A string exportada de configuração fornecida pela infraestrutura Technolife está documentada em [docs/SERVER-CONFIG.md](docs/SERVER-CONFIG.md).

**Nunca devem ser armazenados neste repositório:**

- senha permanente de acesso remoto;
- credenciais administrativas;
- tokens de API administrativos;
- chaves privadas do servidor;
- credenciais SSH;
- qualquer segredo de cliente.

## Arquitetura proposta

A implementação inicial será baseada em .NET 8, com núcleo compartilhado e adaptadores específicos por plataforma.

```text
src/
├── Technolife.RustDesk.Core/
├── Technolife.RustDesk.Cli/
├── Technolife.RustDesk.Windows/
└── Technolife.RustDesk.Platforms/
    ├── Windows/
    ├── Linux/
    └── MacOS/

tests/
docs/
scripts/
```

Responsabilidades principais:

- **Core**: fluxo de configuração, contratos, validação, logging e regras independentes do sistema operacional.
- **Platforms**: descoberta, instalação, caminhos, permissões e execução específicos de Windows, Linux e macOS.
- **Windows UI**: interface mínima obrigatória para o cliente final; duplo clique deve iniciar o fluxo completo sem exigir terminal ou parâmetros.
- **CLI**: interface técnica secundária para suporte, diagnóstico e automação, reutilizando o mesmo Core.

Mais detalhes em [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Estratégia de desenvolvimento

A primeira entrega Windows já cobre um fluxo pequeno e testável:

- detectar RustDesk;
- instalar a versão homologada quando ausente;
- aplicar configuração em uma instalação existente;
- validar;
- gerar logs.

A experiência Windows da RD-007.1 foi validada localmente por duplo clique. O próximo passo, fora desta tarefa, é definir e publicar a `v0.1.0-beta.2` para testes em campo. Linux/macOS permanecem bloqueados.

O planejamento completo está em [docs/ROADMAP.md](docs/ROADMAP.md).

## Particularidades por sistema

### Windows

- primeira plataforma de implementação;
- instalação silenciosa da versão homologada;
- elevação administrativa quando necessária;
- detecção de instalação padrão e possíveis variações.

### Linux

A primeira versão Linux deverá se limitar a Debian/Ubuntu x64. Outras distribuições serão adicionadas apenas após validação específica.

### macOS

A instalação/configuração pode ser automatizada, porém o funcionamento completo do controle remoto depende de permissões do macOS, como:

- Accessibility;
- Screen Recording;
- Input Monitoring, quando necessário.

O configurador deverá detectar e orientar o usuário quando uma permissão exigir ação manual.

## Regras do projeto

1. Não modificar nem recompilar o RustDesk sem necessidade.
2. Preferir interfaces oficialmente suportadas pelo RustDesk.
3. Não editar arquivos internos do RustDesk diretamente quando uma interface suportada resolver o mesmo problema.
4. Manter a lógica de negócio independente do sistema operacional.
5. Não incluir segredos no código-fonte.
6. Toda instalação automática deve validar origem e integridade do pacote.
7. Todo erro relevante deve ser registrado em log.
8. O programa deve ser idempotente: executá-lo novamente não deve danificar uma instalação já configurada.
9. O instalador/configurador não deve definir senha permanente de acesso remoto por padrão.
10. Mudanças de comportamento relevantes devem ser documentadas.

## Documentação para desenvolvimento

- [AGENTS.md](AGENTS.md) — contexto e regras para Codex/agentes de desenvolvimento.
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — arquitetura e responsabilidades.
- [docs/ROADMAP.md](docs/ROADMAP.md) — etapas e tarefas planejadas.
- [docs/SERVER-CONFIG.md](docs/SERVER-CONFIG.md) — configuração pública do servidor.
- [docs/SECURITY.md](docs/SECURITY.md) — regras de segurança e distribuição.

## Interface Windows e CLI técnica

A `v0.1.0-beta.1` ainda inicia como CLI e, sem argumentos, mostra ajuda. Esse comportamento foi reprovado para uso por clientes.

A RD-007.1 criou um executável Windows separado que **abre uma interface gráfica mínima e executa automaticamente o fluxo completo de setup por duplo clique, sem argumentos e sem terminal**. A CLI permanece disponível apenas para suporte técnico e diagnóstico.

Comandos técnicos existentes:

```powershell
dotnet run --project src/Technolife.RustDesk.Cli -- status
dotnet run --project src/Technolife.RustDesk.Cli -- configure
dotnet run --project src/Technolife.RustDesk.Cli -- setup
```

Para desenvolvimento e diagnóstico controlado, é possível substituir o executável detectado e o diretório de logs:

```powershell
dotnet run --project src/Technolife.RustDesk.Cli -- status --rustdesk-path <caminho>
dotnet run --project src/Technolife.RustDesk.Cli -- configure --rustdesk-path <caminho> --log-directory <diretório>
dotnet run --project src/Technolife.RustDesk.Cli -- setup --installer-path <instalador-local> --log-directory <diretório>
```

`configure` exige que o RustDesk já esteja instalado. `setup` detecta a instalação e,
se ela estiver ausente, instala o pacote homologado, detecta novamente, configura e
valida. `--installer-path` é uma substituição explícita para testes controlados; o
caminho não é padrão de produção e o arquivo local passa pela mesma validação SHA-256.

### Pacote RustDesk homologado para Windows

| Campo | Valor |
|---|---|
| Versão | `1.4.9` |
| Plataforma | Windows x64 |
| Arquivo | `rustdesk-1.4.9-x86_64.exe` |
| Origem | [GitHub Releases oficial do RustDesk](https://github.com/rustdesk/rustdesk/releases/download/1.4.9/rustdesk-1.4.9-x86_64.exe) |
| SHA-256 | `EAEDEB0088E687BF46F7C46A9C6EA5493CE51F3134DFD6ACBEDB47B5B9136274` |

O configurador não consulta `latest`. O download usa HTTPS e streaming, e o pacote
só é executado após o SHA-256 corresponder ao manifesto versionado. A instalação usa
o mecanismo oficial `--silent-install`; somente esse processo solicita elevação pelo
UAC. Arquivos temporários ficam sob
`%TEMP%\Technolife\RustDeskConfigurator\<execução>\` e são removidos ao final.

O fluxo é idempotente: se o RustDesk já estiver presente, `setup` não baixa nem
reinstala o pacote e segue diretamente para configuração e validação.

O diretório padrão de produção planejado para logs no Windows é:

```text
%ProgramData%\Technolife\RustDeskConfigurator\logs\
```

O fluxo considera a configuração como `Applied` quando `--config` termina com exit code zero e o executável continua acessível. Isso não equivale a `Verified`: a documentação oficial consultada não documenta uma operação de leitura posterior de todos os campos, e o configurador não lê arquivos internos do RustDesk para simular essa confirmação.

### Códigos de saída

| Código | Resultado |
|---:|---|
| `0` | Fluxo concluído com sucesso |
| `1` | Erro geral |
| `2` | RustDesk não encontrado |
| `3` | Configuração inválida |
| `4` | Falha no processo de configuração |
| `5` | Validação falhou |
| `6` | Plataforma não suportada |
| `7` | Falha de download |
| `8` | Checksum inválido |
| `9` | Falha de instalação ou redetecção |
| `10` | Elevação recusada ou com falha |

## Referências oficiais

- RustDesk Client Configuration: https://rustdesk.com/docs/en/self-host/client-configuration/
- RustDesk Client: https://rustdesk.com/docs/en/client/
- RustDesk macOS: https://rustdesk.com/docs/en/client/mac/

## Estado do projeto

**Fase atual:** RD-007.1 concluída localmente; Beta 2 aguardando etapa própria de publicação.

A `v0.1.0-beta.1` comprovou o motor técnico, mas foi reprovada como artefato para cliente leigo porque o duplo clique sem argumentos apenas exibe ajuda e encerra. Ela permanece publicada somente como referência técnica.

O fluxo corrigido foi testado pelo Explorer com RustDesk 1.4.9 já instalado: a GUI abriu sem terminal, iniciou automaticamente, não reinstalou o RustDesk, aplicou a configuração, validou o resultado e permaneceu na tela de sucesso até o clique em `Concluir`.

O próximo artefato será a `v0.1.0-beta.2`, ainda não publicada. O cenário físico sem RustDesk será validado em uma máquina limpa antes dessa publicação; os caminhos de ausência, instalação e falhas permanecem cobertos por testes automatizados.

RD-008 (Linux Debian/Ubuntu) permanece bloqueada até a Beta 2 passar por teste em campo Windows.


## Acompanhamento do projeto

Esta tabela resume o desenvolvimento do projeto do início até a primeira versão completa planejada. As etapas estão separadas por blocos para deixar claro em qual sistema operacional estamos trabalhando em cada fase.

| Etapa | Fase | Objetivo | Status |
|---|---|---|---|
| **—** | **FASE 0 — FUNDAÇÃO MULTIPLATAFORMA** | **Base comum que será reutilizada por Windows, Linux e macOS** | **🟢 Concluída** |
| RD-001 | Fundação | Criar a solution .NET 8, projetos Core, CLI, Platforms e Tests, referências e build inicial | 🟢 Concluído |
| RD-002 | Core | Definir contratos, modelos e abstrações compartilhadas do configurador | 🟢 Concluído |
| **—** | **FASE 1 — WINDOWS x64** | **Construir e homologar a primeira versão funcional do configurador** | **🟢 Concluída** |
| RD-003 | Windows | Detectar instalações existentes do RustDesk no Windows | 🟢 Concluído |
| RD-004 | Windows | Aplicar a configuração Technolife em RustDesk já instalado usando `--config` | 🟢 Concluído |
| RD-005 | Windows | Implementar validação do fluxo, mensagens de erro, códigos de saída e logs | 🟢 Concluído |
| RD-006 | Windows | Baixar, validar e instalar automaticamente uma versão homologada do RustDesk quando necessário | 🟢 Concluído |
| RD-007 | Windows | Gerar, testar e homologar o primeiro executável Windows x64 | 🟢 Concluído |
| **—** | **MARCO — MOTOR WINDOWS HOMOLOGADO** | **Motor de detecção, instalação, configuração, validação e logs concluído** | **🟢 Concluído localmente** |
| **—** | **CORREÇÃO UX WINDOWS** | **Adequar o executável ao uso por cliente leigo antes do teste em campo** | **🟢 Concluída localmente** |
| RD-007.1 | Windows — experiência do cliente | Duplo clique executa setup automaticamente em GUI mínima, sem terminal ou parâmetros | 🟢 Concluída localmente |
| **—** | **FASE — TESTES EM CAMPO WINDOWS** | **Distribuir v0.1.0-beta.2 em máquinas reais somente após concluir RD-007.1** | **⚪ Aguardando Beta 2** |
| **—** | **FASE 2 — LINUX** | **Reutilizar o Core validado e adaptar instalação/configuração ao ecossistema Linux** | **⚪ Planejada** |
| RD-008 | Linux Debian/Ubuntu | Implementar e homologar suporte inicial x64 | ⚪ Planejada |
| RD-009 | Linux — expansão | Adicionar outras distribuições e formatos conforme demanda real | ⚪ Futuro |
| **—** | **MARCO — LINUX HOMOLOGADO** | **Disponibilizar build Linux suportado oficialmente pelo projeto** | **⚪ Planejado** |
| **—** | **FASE 3 — macOS** | **Adaptar o Core ao macOS e tratar permissões específicas do sistema** | **⚪ Planejado** |
| RD-010 | macOS Intel | Implementar e homologar suporte x64 | ⚪ Planejado |
| RD-011 | macOS Apple Silicon | Implementar e homologar suporte arm64 | ⚪ Planejado |
| **—** | **MARCO — macOS HOMOLOGADO** | **Disponibilizar builds para Intel e Apple Silicon** | **⚪ Planejado** |
| **—** | **FASE 4 — EXPERIÊNCIA E AUTOMAÇÃO** | **Transformar os builds funcionais em um produto simples de distribuir e utilizar** | **⚪ Planejado** |
| RD-012 | Interface avançada | Evoluir a GUI mínima já exigida no Windows para uma experiência mais completa, sem mover regras do Core | ⚪ Planejado |
| RD-013 | Automação | Configurar GitHub Actions para build e testes multiplataforma | ⚪ Planejado |
| RD-014 | Distribuição | Padronizar releases, downloads, checksums e matriz de compatibilidade | ⚪ Planejado |
| **—** | **FASE 5 — RELEASE ESTÁVEL** | **Homologação final do produto multiplataforma** | **⚪ Planejado** |
| Release 1.0 | Conclusão | Publicar a primeira versão estável e homologada para as plataformas suportadas | ⚪ Planejado |

Legenda: 🟢 concluído · 🟡 em andamento · ⚪ planejado/futuro

O detalhamento técnico de cada etapa continua disponível em [docs/ROADMAP.md](docs/ROADMAP.md).

