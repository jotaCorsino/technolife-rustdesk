# Downloads

Esta página é o índice de builds publicados do Technolife RustDesk Configurator. Builds de teste e versões estáveis são identificados separadamente.

## Builds publicados

Ainda não existem builds estáveis publicados.

| Plataforma | Arquitetura | Arquivo | Versão | Status | Download |
|---|---|---|---|---|---|
| Windows 10/11 | x64 | `Technolife-RustDesk-Windows.exe` | `v0.1.0-beta.2` | 🟡 PRE-RELEASE / TESTE EM CAMPO | [Release de testes](https://github.com/jotaCorsino/technolife-rustdesk/releases/tag/v0.1.0-beta.2) |
| Linux Debian/Ubuntu | x64 | `technolife-rustdesk-linux` | — | Planejado | — |
| macOS | Intel x64 | `Technolife-RustDesk-macOS-x64` | — | Planejado | — |
| macOS | Apple Silicon arm64 | `Technolife-RustDesk-macOS-arm64` | — | Planejado | — |

## Beta Windows x64 — v0.1.0-beta.2 para testes em campo

> **Pre-release destinada exclusivamente a testes externos.** Não é uma versão estável, homologada para produção ou release final.

- [Baixar EXE](https://github.com/jotaCorsino/technolife-rustdesk/releases/download/v0.1.0-beta.2/Technolife-RustDesk-Windows.exe)
- [Baixar checksum SHA-256](https://github.com/jotaCorsino/technolife-rustdesk/releases/download/v0.1.0-beta.2/Technolife-RustDesk-Windows.exe.sha256)
- [Abrir GitHub Release](https://github.com/jotaCorsino/technolife-rustdesk/releases/tag/v0.1.0-beta.2)

A Beta 2 inclui:

- GUI Windows para cliente final e setup automático por duplo clique;
- uma única confirmação UAC no fluxo normal;
- instalação automática do RustDesk 1.4.9 quando ausente;
- instalação e ativação automática do serviço `RustDesk`;
- aplicação administrativa da configuração Technolife;
- releitura e validação real de ID Server, Relay Server e chave pública;
- bloqueio do falso positivo em que `--config` retornava zero sem aplicar os valores;
- execução idempotente;
- 119 testes automatizados aprovados.

O cenário real em uma máquina Windows limpa sem RustDesk faz parte dos testes externos
desta pre-release. O executável ainda não possui assinatura Authenticode e o Windows
SmartScreen pode apresentar aviso.

```text
Arquivo: Technolife-RustDesk-Windows.exe
Plataforma: Windows 10/11 x64
Versão: v0.1.0-beta.2
RustDesk homologado: 1.4.9
Status: PRE-RELEASE / TESTE EM CAMPO
Formato: self-contained, single-file, win-x64
Tamanho: 161860760 bytes
SHA-256: FC74847EBE7A8ED6A95F086588713A0DAB50DC17C0137F0B0653D94966323EDC
```

## Windows x64 — v0.1.0-beta.2

```text
Arquivo: Technolife-RustDesk-Windows.exe
Plataforma: Windows 10/11 x64
Versão: v0.1.0-beta.2
RustDesk homologado: 1.4.9
Status: PRE-RELEASE / TESTE EM CAMPO
Formato: self-contained, single-file, win-x64
SHA-256: FC74847EBE7A8ED6A95F086588713A0DAB50DC17C0137F0B0653D94966323EDC
```

- [Baixar EXE](https://github.com/jotaCorsino/technolife-rustdesk/releases/download/v0.1.0-beta.2/Technolife-RustDesk-Windows.exe)
- [Baixar checksum SHA-256](https://github.com/jotaCorsino/technolife-rustdesk/releases/download/v0.1.0-beta.2/Technolife-RustDesk-Windows.exe.sha256)
- [Abrir GitHub Release](https://github.com/jotaCorsino/technolife-rustdesk/releases/tag/v0.1.0-beta.2)

Esta pre-release inclui interface gráfica para cliente final, execução automática por
duplo clique, uma única confirmação UAC, instalação automática do RustDesk 1.4.9,
criação e ativação do serviço `RustDesk`, configuração administrativa e validação
real de ID Server, Relay Server e chave pública.

O executável ainda não possui assinatura Authenticode e poderá apresentar aviso do
SmartScreen. O cenário de instalação em máquinas sem RustDesk faz parte dos testes em campo.

## Como atualizar esta página

Quando uma versão for homologada:

1. publicar os artefatos em GitHub Releases;
2. calcular/publicar SHA-256;
3. preencher versão, link e checksum nesta tabela;
4. atualizar a seção Downloads do README;
5. registrar sistemas operacionais efetivamente testados nas notas da release.

## Política

Um arquivo só deve aparecer como "estável" após:

- build reproduzível;
- testes automatizados aplicáveis;
- teste manual na plataforma;
- validação de conexão ao servidor Technolife;
- verificação de checksum;
- confirmação de que o pacote não contém credenciais privadas.

## Candidatos

Builds de teste podem ser publicados como prerelease no GitHub Releases, mas não devem substituir os links estáveis até a homologação.
