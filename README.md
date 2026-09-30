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
| Windows 10/11 | x64 | Planejado para a primeira implementação |
| Linux Debian/Ubuntu | x64 | Planejado |
| macOS | Intel x64 | Planejado |
| macOS | Apple Silicon arm64 | Planejado |
| Outras distribuições Linux | A definir | Futuro |

O projeto será desenvolvido como **um produto, um repositório e uma base de código**, com builds específicos por sistema operacional.

## Downloads

Esta seção será atualizada quando os primeiros builds forem homologados.

| Sistema | Build | Status | Download |
|---|---|---|---|
| Windows x64 | `Technolife-RustDesk-Windows.exe` | Em desenvolvimento | — |
| Linux x64 | `technolife-rustdesk-linux` | Planejado | — |
| macOS Intel | `Technolife-RustDesk-macOS-x64` | Planejado | — |
| macOS Apple Silicon | `Technolife-RustDesk-macOS-arm64` | Planejado | — |

Quando existirem releases publicados, os links oficiais deverão apontar para a área **Releases** deste repositório.

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
- **CLI**: primeira interface do produto. Uma interface gráfica poderá ser adicionada posteriormente sem alterar o núcleo.

Mais detalhes em [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Estratégia de desenvolvimento

A primeira entrega deverá priorizar um fluxo pequeno e testável:

- detectar RustDesk;
- aplicar configuração em uma instalação existente;
- validar;
- gerar logs.

Somente depois serão adicionadas instalação automática, builds multiplataforma, interface gráfica e automações de release.

O planejamento completo está em [docs/ROADMAP.md](docs/ROADMAP.md).

## Particularidades por sistema

### Windows

- primeira plataforma de implementação;
- suporte planejado a instalação silenciosa;
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

## Referências oficiais

- RustDesk Client Configuration: https://rustdesk.com/docs/en/self-host/client-configuration/
- RustDesk Client: https://rustdesk.com/docs/en/client/
- RustDesk macOS: https://rustdesk.com/docs/en/client/mac/

## Estado do projeto

**Fase atual:** documentação e fundação.

Ainda não há build de produção homologado.


## Acompanhamento do projeto

Esta tabela resume o desenvolvimento do projeto do início até a primeira versão completa planejada.

| Etapa | Fase | Objetivo | Status |
|---|---|---|---|
| RD-001 | Fundação | Criar a solution .NET 8, projetos Core, CLI, Platforms e Tests, referências e build inicial | 🟡 Em andamento |
| RD-002 | Core | Definir contratos, modelos e abstrações compartilhadas do configurador | ⚪ Planejado |
| RD-003 | Windows MVP | Detectar instalações existentes do RustDesk no Windows | ⚪ Planejado |
| RD-004 | Windows MVP | Aplicar a configuração Technolife em RustDesk já instalado usando `--config` | ⚪ Planejado |
| RD-005 | Windows MVP | Implementar validação do fluxo, mensagens de erro, códigos de saída e logs | ⚪ Planejado |
| RD-006 | Windows MVP | Baixar, validar e instalar automaticamente uma versão homologada do RustDesk quando necessário | ⚪ Planejado |
| RD-007 | Windows MVP | Gerar e homologar o primeiro executável Windows x64 | ⚪ Planejado |
| RD-008 | Linux | Implementar suporte inicial para Debian/Ubuntu x64 | ⚪ Planejado |
| RD-009 | Linux | Expandir suporte para outras distribuições e formatos, conforme demanda | ⚪ Futuro |
| RD-010 | macOS | Implementar suporte para macOS Intel x64 | ⚪ Planejado |
| RD-011 | macOS | Implementar suporte para macOS Apple Silicon arm64 | ⚪ Planejado |
| RD-012 | Experiência | Criar interface gráfica usando o mesmo Core já validado | ⚪ Planejado |
| RD-013 | Automação | Configurar GitHub Actions para build e testes multiplataforma | ⚪ Planejado |
| RD-014 | Distribuição | Padronizar releases, downloads, checksums e matriz de compatibilidade | ⚪ Planejado |
| Release 1.0 | Conclusão | Publicar a primeira versão estável e homologada para as plataformas suportadas | ⚪ Planejado |

Legenda: 🟢 concluído · 🟡 em andamento · ⚪ planejado/futuro

O detalhamento técnico de cada etapa continua disponível em [docs/ROADMAP.md](docs/ROADMAP.md).
