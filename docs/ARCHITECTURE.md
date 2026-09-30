# Arquitetura

## Visão geral

O Technolife RustDesk Configurator será uma aplicação multiplataforma com núcleo compartilhado e adaptadores de plataforma.

```text
                     Technolife RustDesk Configurator
                                  │
                       ┌──────────┴──────────┐
                       │                     │
                 CLI técnica           Windows UI
                       │                     │
                       └──────────┬──────────┘
                                  │
                                 Core
                                  │
                 ┌────────────────┼────────────────┐
                 │                │                │
              Windows           Linux            macOS
                 │                │                │
                 └────────────────┼────────────────┘
                                  │
                              RustDesk
                                  │
                       Servidor Technolife
```

## Core

Responsável por:

- orquestrar o fluxo;
- representar estado e resultado;
- contratos de detector, instalador, configurador, launcher e validador;
- política de versão homologada;
- logging abstrato;
- tratamento de erros independente de plataforma.

Interfaces candidatas:

```text
IRustDeskDetector
IRustDeskInstaller
IRustDeskConfigurator
IRustDeskLauncher
IRustDeskValidator
IPlatformEnvironment
IProcessRunner
IDownloadClient
IFileIntegrityValidator
```

Os nomes são propostas iniciais e podem ser refinados durante RD-001/RD-002.

## Camada de plataforma

### Windows

Responsável por:

- caminhos padrão e alternativos;
- elevação;
- instalação silenciosa;
- encerramento/reinício do RustDesk;
- detalhes de serviço/processo;
- publicação `win-x64`.

Na detecção inicial, os caminhos candidatos são avaliados nesta ordem:

1. `C:\Program Files\RustDesk\RustDesk.exe`;
2. `C:\Program Files (x86)\RustDesk\RustDesk.exe`.

O primeiro caminho é o diretório padrão indicado pela [documentação oficial do cliente](https://rustdesk.com/docs/en/client/). O segundo contempla a seleção de Program Files para processos de 32 bits observada na [implementação oficial do RustDesk](https://github.com/rustdesk/rustdesk/blob/master/src/platform/windows.rs).

O acesso ao arquivo é isolado por `IFileProbe`, permitindo testes sem disco real. A versão é lida dos metadados do executável sem iniciá-lo; falhas nessa leitura não invalidam uma instalação encontrada. Até existir inspeção confiável do binário, sua arquitetura é representada como `Unknown`.

### Linux

Primeiro alvo: Debian/Ubuntu x64.

Responsável por:

- descoberta do binário;
- identificação de distribuição;
- permissões via sudo quando necessárias;
- formato/política de instalação homologada;
- publicação `linux-x64`.

A v1 não deverá tentar suportar automaticamente todas as distribuições Linux.

### macOS

Responsável por:

- localização de `RustDesk.app`;
- Intel e Apple Silicon;
- instalação homologada;
- execução;
- detecção/orientação de permissões.

Permissões de Accessibility, Screen Recording e, quando necessário, Input Monitoring podem exigir interação do usuário e não devem ser tratadas como silenciosamente automatizáveis sem confirmação técnica.

## Configuração RustDesk

Método preferencial:

```text
rustdesk --config "<CONFIG_STRING>"
```

No Windows:

```text
rustdesk.exe --config "<CONFIG_STRING>"
```

A implementação deve passar argumentos por API adequada de processos, evitando construir uma única string de shell sempre que possível.

Na implementação Windows, `WindowsRustDeskConfigurator` recebe a instalação detectada e a configuração de runtime, valida as pré-condições e delega a execução a `IProcessRunner`. A fonte de runtime `TechnolifeRustDeskConfiguration` concentra os valores públicos usados pelo cliente sem introduzir senhas, tokens ou chaves privadas.

`SystemProcessRunner` chama diretamente o executável com `ProcessStartInfo.ArgumentList`, captura `stdout`, `stderr` e exit code, respeita timeout e converte falhas de inicialização em resultados estruturados. Os argumentos permanecem separados e sua representação textual é redigida; a configuração exportada não é propagada em mensagens ou detalhes técnicos do configurador.

Esse fluxo segue o mecanismo `--config` descrito na [documentação oficial de configuração do cliente RustDesk](https://rustdesk.com/docs/en/self-host/client-configuration/). A RD-004 interpreta a conclusão do processo e seu exit code, mas a validação funcional pós-configuração e o logging completo pertencem à RD-005.

## Workflow de configuração

`RustDeskConfigurationWorkflow`, no Core, coordena as dependências sem conhecer detalhes de Windows:

```text
detectar → configurar → validar → registrar → retornar resultado
```

O workflow interrompe imediatamente após uma falha, converte exceções inesperadas em resultado estruturado e remove a string exportada de detalhes antes de registrá-los ou devolvê-los. A CLI apenas compõe as implementações Windows e apresenta o resultado; a lógica do fluxo não fica na interface.

## Validação pós-configuração

`WindowsRustDeskValidator` confirma que a instalação usada continua declarada como Windows e que seu executável permanece acessível após o sucesso de `--config`.

O resultado atual é `Applied`, não `Verified`. Exit code zero confirma a conclusão do comando, mas a documentação oficial consultada não documenta uma operação capaz de reler e comparar todos os campos importados. A implementação não acessa TOML, Registro do Windows ou outros detalhes internos para elevar artificialmente esse nível de confiança.

## Instalação

A instalação e a configuração são responsabilidades diferentes.

```text
RustDesk já instalado
    → configurar

RustDesk ausente
    → instalar
    → configurar
```

Isso permite usar o configurador mesmo em máquinas onde o RustDesk foi instalado manualmente, por RMM ou por outro método.

No Windows x64, `RustDeskSetupWorkflow` coordena o fluxo explícito do comando
`setup`. Quando a detecção inicial não encontra o RustDesk, o
`WindowsRustDeskInstaller` obtém o pacote fixado, valida seu SHA-256, chama o
instalador com o argumento separado `--silent-install` e solicita elevação apenas
para esse processo por meio de `runas`. O prompt do UAC permanece sob controle do
Windows e pode ser recusado pelo usuário.

O pacote é preparado em
`%TEMP%\Technolife\RustDeskConfigurator\<identificador-único>\`. O diretório de cada
execução é removido em bloco `finally`; uma falha de limpeza gera apenas aviso seguro.
Depois de exit code zero, o detector tenta localizar novamente a instalação antes de
permitir configuração. Sucesso do instalador sem redetecção é tratado como falha.

`HttpDownloadClient` exige HTTPS, recebe a resposta em streaming e grava primeiro em
arquivo parcial com nome único. Somente um download completo é promovido ao destino.
`Sha256FileIntegrityValidator` calcula o hash por streaming e a execução é bloqueada
se houver divergência. `--installer-path` troca apenas a aquisição remota por uma
cópia local de teste; a validação de integridade e todas as etapas posteriores são as
mesmas.

## Política de versões

Não usar "latest" como comportamento de produção sem controle.

Cada release do configurador deverá apontar para uma versão de RustDesk homologada por plataforma.

Futuramente essa política poderá vir de um manifesto semelhante a:

```json
{
  "windows-x64": {
    "version": "x.y.z",
    "url": "...",
    "sha256": "..."
  }
}
```

O manifesto Windows inicial está versionado em `WindowsRustDeskPackageManifest`:

```text
Versão: 1.4.9
Arquitetura: Windows x64
URL: https://github.com/rustdesk/rustdesk/releases/download/1.4.9/rustdesk-1.4.9-x86_64.exe
SHA-256: EAEDEB0088E687BF46F7C46A9C6EA5493CE51F3134DFD6ACBEDB47B5B9136274
```

Uma atualização exige alteração explícita desse manifesto e nova homologação; não há
resolução automática de `latest`.

## Logging

Logs devem conter:

- versão do configurador;
- sistema operacional e arquitetura;
- versão detectada do RustDesk;
- etapa executada;
- exit code de processos relevantes;
- erro técnico sem expor segredos.

Não registrar senhas, tokens ou material secreto.

O contrato `IAppLogger` mantém o Core independente de console e filesystem. `FileAppLogger` grava um arquivo por execução, com timestamp no nome, níveis `INFO`, `WARN` e `ERROR`, normalização de linhas e redação defensiva dos valores sensíveis informados.

No Windows, o diretório padrão é:

```text
%ProgramData%\Technolife\RustDeskConfigurator\logs\
```

A CLI aceita `--log-directory` para testes e diagnósticos controlados sem alterar esse padrão de produção.

## Interface Windows, CLI técnica e códigos de saída

A experiência principal do cliente Windows é uma interface gráfica mínima. A partir da RD-007.1, executar o artefato normalmente por duplo clique, sem argumentos, deve iniciar o fluxo completo equivalente a `setup` e apresentar progresso, sucesso ou erro sem exibir terminal.

A CLI permanece como interface técnica secundária. Ela expõe `status` para detecção sem alteração, `configure` para configurar uma instalação existente e `setup` para instalar quando necessário e então configurar. Esses comandos continuam úteis para suporte, diagnóstico, testes e automação.

| Código | Significado |
|---:|---|
| `0` | sucesso |
| `1` | erro geral |
| `2` | RustDesk não encontrado |
| `3` | configuração inválida |
| `4` | falha de processo |
| `5` | falha de validação |
| `6` | plataforma não suportada |
| `7` | falha de download |
| `8` | checksum inválido |
| `9` | falha de instalação ou redetecção |
| `10` | elevação recusada ou com falha |

## Idempotência

Executar o configurador duas ou mais vezes deve ser seguro.

Exemplos:

- não reinstalar RustDesk desnecessariamente;
- reaplicar a configuração sem corromper estado;
- não duplicar componentes;
- não produzir erro apenas porque o estado desejado já existe.

## Interface

A CLI foi utilizada para construir e validar o motor técnico, mas não representa a experiência final do cliente Windows.

A interface mínima Windows é requisito do produto:

```text
duplo clique
→ GUI Technolife
→ setup automático
→ progresso
→ sucesso/erro
```

A GUI deve ser fina: apenas iniciar e observar os workflows existentes, traduzir estados para mensagens simples e oferecer ações básicas como concluir ou tentar novamente. Nenhuma regra de detecção, instalação, download, checksum, configuração ou validação deve ser copiada para a UI.

A CLI continua disponível como ferramenta técnica secundária. Uma interface avançada poderá ser desenvolvida posteriormente sobre o mesmo Core.

## Distribuição

Artefatos planejados:

```text
Technolife-RustDesk-Windows.exe
technolife-rustdesk-linux
Technolife-RustDesk-macOS-x64
Technolife-RustDesk-macOS-arm64
```

Os releases deverão ser publicados no GitHub Releases após homologação.
