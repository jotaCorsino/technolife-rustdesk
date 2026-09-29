# AGENTS.md

Este arquivo orienta Codex e outros agentes de desenvolvimento que trabalhem neste repositório.

## Contexto do produto

O Technolife RustDesk Configurator existe para reduzir o trabalho manual necessário para preparar o RustDesk nos computadores de clientes da Technolife.

O produto deve:

- funcionar em múltiplos sistemas operacionais;
- detectar se o RustDesk já está instalado;
- instalar o RustDesk quando necessário e quando a plataforma suportar esse fluxo;
- aplicar a configuração do servidor Technolife;
- validar o resultado;
- registrar logs;
- orientar o usuário quando alguma ação manual for inevitável.

A estratégia é manter **uma base de código compartilhada** e gerar **artefatos específicos por sistema operacional**.

## Decisões arquiteturais já tomadas

- Linguagem/plataforma inicial: C# / .NET 8.
- Primeira interface: CLI simples.
- Primeira plataforma de implementação: Windows x64.
- Linux inicial: Debian/Ubuntu x64.
- macOS deverá contemplar Intel x64 e Apple Silicon arm64.
- O núcleo deve ser independente de sistema operacional.
- Código específico de plataforma deve ficar isolado.
- Não criar um fork do RustDesk para a v1.
- Priorizar comandos e mecanismos oficialmente suportados pelo RustDesk.
- A aplicação da configuração deverá priorizar `--config`.
- O programa deve ser idempotente.

## Infraestrutura Technolife

Configuração pública:

```text
ID Server: remoto.technolife.net.br
Relay Server: remoto.technolife.net.br
Public Key: mFwxUtIrUDZkAI3zvh4RrvJIMlipFye+ILMbwtdK8jM=
```

A string exportada está em `docs/SERVER-CONFIG.md`.

## Restrições de segurança

Nunca adicionar ao repositório:

- senha permanente do RustDesk;
- senha de usuário ou administrador;
- tokens administrativos;
- chaves privadas;
- credenciais SSH;
- segredos de clientes.

A chave RustDesk documentada neste projeto é a chave pública de conexão do servidor.

Qualquer funcionalidade futura que envolva segredo deve usar um mecanismo externo de provisionamento ou armazenamento seguro e deverá ser projetada separadamente.

## Comportamento esperado

Fluxo base:

```text
iniciar
  ↓
detectar plataforma
  ↓
detectar RustDesk
  ├─ instalado → localizar executável
  └─ ausente → instalar, quando suportado
  ↓
aplicar configuração Technolife
  ↓
validar
  ↓
iniciar/reiniciar RustDesk quando necessário
  ↓
registrar resultado
  ↓
exibir sucesso ou instrução de correção
```

## Estrutura pretendida

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

Não criar dependências da camada Core para APIs específicas de Windows, Linux ou macOS.

## Ordem de implementação

Seguir `docs/ROADMAP.md`.

Não avançar automaticamente para todas as plataformas de uma vez. Cada etapa deve ser testável e revisável.

Prioridade inicial:

1. Fundação .NET.
2. Contratos do Core.
3. Detector Windows.
4. Configuração Windows em RustDesk já instalado.
5. Validação e logs.
6. Instalação automática Windows.
7. Publicação Windows.
8. Linux.
9. macOS.
10. GUI e automação de releases.

## Regras de implementação

- Métodos devem ter responsabilidade clara.
- Processos externos devem capturar exit code, stdout e stderr quando possível.
- Caminhos de executáveis não devem ser espalhados pelo código.
- Comandos externos devem ser abstraídos e testáveis.
- Não interpolar argumentos de processo de forma insegura.
- Não assumir que o usuário possui privilégios administrativos.
- Falhas devem produzir mensagens compreensíveis e logs técnicos.
- Não apagar configurações existentes sem necessidade.
- Antes de instalar, verificar versão/estado existente.
- Download automático deve usar origem oficial ou explicitamente homologada.
- Quando houver download, validar integridade do artefato.
- Não baixar sempre a versão mais recente sem política de homologação.
- Preferir versão fixa/homologada configurável.

## Critérios mínimos de teste

Cada plataforma deverá ter testes para:

- RustDesk ausente;
- RustDesk encontrado;
- caminho alternativo;
- falha ao executar processo;
- configuração aplicada;
- configuração já aplicada;
- falta de privilégio;
- pacote inválido;
- rede indisponível durante instalação;
- logs gerados.

Operações destrutivas ou que alterem o sistema real não devem ocorrer em testes unitários.

## Commits

Preferir commits pequenos e objetivos.

Exemplos:

```text
chore: cria fundação .NET do configurador
feat: detecta instalação do RustDesk no Windows
feat: aplica configuração Technolife via --config
test: adiciona testes do detector Windows
docs: atualiza suporte Linux
fix: trata caminho alternativo do RustDesk
```

## Definition of Done

Uma tarefa só deve ser considerada concluída quando:

- implementação estiver completa;
- testes relevantes passarem;
- documentação afetada estiver atualizada;
- não houver segredo introduzido;
- comportamento de erro estiver tratado;
- build da plataforma afetada estiver funcionando.

## Fontes oficiais

Antes de implementar comportamento dependente do RustDesk, confirmar a documentação oficial atual:

- https://rustdesk.com/docs/en/self-host/client-configuration/
- https://rustdesk.com/docs/en/client/
- https://rustdesk.com/docs/en/client/windows/
- https://rustdesk.com/docs/en/client/mac/

Não depender de comportamento não documentado sem registrar explicitamente a decisão.
