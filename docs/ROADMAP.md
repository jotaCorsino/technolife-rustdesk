# Roadmap de desenvolvimento

## Estratégia

O desenvolvimento será incremental. A primeira meta não é criar um instalador universal completo, e sim provar com segurança o fluxo essencial no Windows e reutilizar o núcleo nas demais plataformas.

## Fase 0 — Fundação

### RD-001 — Fundação do projeto

**Status: concluída.**

Objetivo: criar a solution .NET 8 e a estrutura inicial.

Entregas:

- solution;
- projeto Core;
- projeto CLI;
- projeto de plataformas;
- projeto de testes;
- configuração de build;
- `.gitignore`;
- primeira execução da CLI;
- documentação atualizada.

Critério de aceite:

- `dotnet build` concluído sem erro;
- `dotnet test` executável;
- CLI inicia e identifica versão do configurador.

### RD-002 — Contratos do Core

**Status: concluída.**

Objetivo: definir abstrações sem implementar detalhes de SO.

Entregas:

- contratos de detecção;
- configuração;
- instalação;
- execução;
- validação;
- execução de processos;
- modelos de resultado/erro;
- testes unitários básicos.

## Fase 1 — Windows MVP

### RD-003 — Detector Windows

**Status: concluída.**

Objetivo: localizar instalações existentes do RustDesk no Windows.

Deve considerar:

- caminho padrão;
- variações justificadas pela documentação/testes;
- versão encontrada;
- arquitetura quando relevante.

Não instalar nada nesta tarefa.

### RD-004 — Aplicação da configuração

**Status: concluída.**

Objetivo: aplicar a configuração Technolife em RustDesk já instalado.

Método prioritário:

```text
rustdesk.exe --config "<CONFIG_STRING>"
```

Entregas:

- execução segura do processo;
- captura de resultado;
- tratamento de erros;
- testes com process runner simulado.

Implementação concluída com chamada direta ao executável, argumentos separados por `ProcessStartInfo.ArgumentList`, timeout, captura de saída e proteção da configuração exportada em mensagens e representações textuais.

### RD-005 — Validação e logs

**Status: concluída.**

Objetivo: confirmar que o fluxo terminou corretamente e produzir diagnóstico útil.

Entregas:

- logging;
- códigos de saída da CLI;
- mensagens amigáveis;
- registro técnico;
- política de não registrar segredos.

Implementação concluída com workflow `detectar → configurar → validar`, logging em arquivo, mensagens amigáveis e códigos de saída estáveis. A validação atual usa o estado `Applied`: confirma o sucesso do processo e a permanência do executável, sem alegar leitura independente dos campos configurados.

### RD-006 — Instalação Windows

**Status: concluída.**

Objetivo: instalar RustDesk quando ausente.

Entregas:

- versão homologada;
- origem oficial/homologada;
- checksum;
- download;
- instalação silenciosa;
- redetecção;
- configuração após instalação;
- tratamento de falta de internet e privilégio.

Implementação concluída para Windows x64 com RustDesk `1.4.9` fixado em manifesto,
origem oficial, SHA-256 obrigatório, download HTTPS em streaming, instalação oficial
via `--silent-install`, elevação pontual pelo UAC, redetecção e continuidade para
configuração e validação. O comando `setup` não reinstala quando o RustDesk já está
presente e aceita `--installer-path` somente para testes locais controlados.

### RD-007 — Publicação Windows

**Status: concluída.**

Objetivo: gerar primeiro artefato utilizável.

Entregas:

- publicação self-contained `win-x64`;
- nome padronizado;
- instrução de uso;
- checklist manual;
- release candidata.

Implementação concluída com publicação `Release` para `win-x64`, self-contained e
single-file, sem trimming ou ReadyToRun. O script `scripts/publish-windows.ps1`
executa restore, build, testes, publish, padroniza o nome
`Technolife-RustDesk-Windows.exe` e gera seu SHA-256.

O executável publicado foi validado localmente com ajuda, `--version`, `status`,
`configure` e `setup`; o último confirmou que uma instalação existente não é baixada
nem reinstalada. O artefato permanece fora do Git e ainda não possui assinatura
Authenticode. A pre-release pública `v0.1.0-beta.1` disponibiliza o executável e seu
checksum para testes em campo. Como Windows Sandbox não está instalado na máquina de
desenvolvimento, o teste em um Windows limpo permanece pendente.

## Correção prioritária — RD-007.1 — Experiência Windows para cliente final

**Status: 🟢 Concluída localmente.**

A `v0.1.0-beta.1` validou o motor técnico, porém foi reprovada para uso por cliente final: ao abrir o executável por duplo clique, sem argumentos, a aplicação mostra ajuda em terminal e encerra. Isso contraria o requisito central do produto.

**Objetivo obrigatório:** o cliente deve baixar um único EXE, dar duplo clique e acompanhar todo o processo sem PowerShell, parâmetros ou conhecimento técnico.

Fluxo alvo:

```text
duplo clique
→ janela Technolife
→ detectar RustDesk
→ instalar se necessário
→ aplicar configuração Technolife
→ validar
→ mostrar sucesso ou erro
→ concluir
```

Entregas enxutas:

- execução sem argumentos inicia automaticamente o fluxo equivalente a `setup`;
- GUI Windows mínima com estados de progresso, sucesso e erro;
- nenhuma janela de terminal no fluxo normal do cliente;
- UAC somente quando a instalação do RustDesk exigir elevação;
- reutilização integral do Core, detector, download, checksum, instalador, configurador, validator e logger existentes;
- comandos `status`, `configure` e `setup` preservados como interface técnica;
- testes para RustDesk presente/ausente, UAC recusado, falha de internet e execução repetida;
- preparação do artefato gráfico candidato à futura `v0.1.0-beta.2`.

A RD-007.1 **não** deve redesenhar o Core nem reimplementar RD-003 a RD-006.

Implementação concluída com o projeto WinForms `Technolife.RustDesk.Windows`, publicado
como `WinExe` self-contained e single-file. A janela inicia o setup automaticamente,
observa o progresso opcional do Core e apresenta sucesso persistente ou erro com
`Tentar novamente` e `Fechar`. A CLI técnica permanece separada e funcional.

O teste real pelo Explorer foi aprovado com RustDesk 1.4.9 existente: nenhuma janela
de terminal foi aberta, não houve reinstalação, a configuração terminou com exit code
zero, a validação retornou `Applied` e `Concluir` fechou a aplicação. O cenário físico
sem RustDesk não foi executado nesta máquina para evitar uma desinstalação destrutiva;
ele permanece obrigatório em máquina limpa antes da Beta 2. A Beta 2 não foi publicada
nesta tarefa.

Detalhamento: [RD-007.1-WINDOWS-UX.md](RD-007.1-WINDOWS-UX.md).

## FASE — TESTES EM CAMPO WINDOWS

**Status: ⚪ Aguardando Beta 2.**

A `v0.1.0-beta.1` permanece apenas como referência técnica e não deve ser distribuída a clientes finais. Os testes em campo serão retomados com a `v0.1.0-beta.2` após a conclusão da RD-007.1.

A RD-008 continua planejada e **não deve ser iniciada antes da validação da experiência Windows corrigida**.

## Fase 2 — Linux

### RD-008 — Linux Debian/Ubuntu

**Status: planejada.**

Objetivo: reutilizar o Core no primeiro alvo Linux.

Entregas:

- detecção Debian/Ubuntu;
- localização do RustDesk;
- configuração;
- instalação homologada;
- privilégios;
- logs;
- testes;
- publicação `linux-x64`.

Não expandir para Fedora/Arch/openSUSE nesta tarefa.

### RD-009 — Expansão Linux

Somente após demanda real e validação.

Alvos candidatos:

- Fedora/Rocky;
- openSUSE;
- Arch;
- AppImage;
- Flatpak.

Cada método deve ser avaliado separadamente.

## Fase 3 — macOS

### RD-010 — macOS Intel

Entregas:

- detecção;
- configuração;
- fluxo de instalação homologado;
- orientação de permissões;
- publicação `osx-x64`.

### RD-011 — macOS Apple Silicon

Entregas:

- suporte `osx-arm64`;
- testes em Apple Silicon;
- mesmos critérios de configuração e permissões.

## Fase 4 — Experiência e distribuição

### RD-012 — Interface gráfica avançada

Objetivo: evoluir a interface mínima obrigatória introduzida na RD-007.1 sem mover lógica para a UI.

Exemplo de estados:

```text
[✓] RustDesk encontrado
[✓] Configuração aplicada
[✓] Validação concluída

RustDesk configurado com sucesso.
```

### RD-013 — GitHub Actions

Objetivo: automatizar build e testes.

Inicialmente:

- build;
- test;
- artefatos por plataforma.

Publicação automática de release só após política de assinatura e homologação.

### RD-014 — Releases e downloads

Objetivo: padronizar distribuição.

Entregas:

- versão semântica;
- checksums;
- notas de release;
- links no README;
- matriz de compatibilidade.

## Fase 5 — Evoluções possíveis

Itens fora do escopo inicial:

- assinatura digital de executáveis;
- notarização macOS;
- MSI;
- pacote `.deb`;
- atualização automática;
- GUI avançada;
- integração com RMM;
- telemetria corporativa, somente se houver definição explícita de privacidade;
- suporte adicional a arquiteturas;
- manifesto remoto de versões homologadas.

## Regra de progressão

Não iniciar uma nova plataforma antes de o fluxo fundamental estar estável na plataforma anterior, salvo necessidade operacional explícita.

O objetivo é evitar três implementações incompletas e manter o Core reutilizável.
