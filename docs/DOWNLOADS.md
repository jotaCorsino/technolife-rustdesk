# Downloads

Esta página será o índice oficial de builds homologados do Technolife RustDesk Configurator.

## Builds públicos estáveis

Ainda não existem builds estáveis publicados.

| Plataforma | Arquitetura | Arquivo planejado | Versão | Status | Download |
|---|---|---|---|---|---|
| Windows | x64 | `Technolife-RustDesk-Windows.exe` | `0.1.0-dev` | Build homologado localmente | — |
| Linux Debian/Ubuntu | x64 | `technolife-rustdesk-linux` | — | Planejado | — |
| macOS | Intel x64 | `Technolife-RustDesk-macOS-x64` | — | Planejado | — |
| macOS | Apple Silicon arm64 | `Technolife-RustDesk-macOS-arm64` | — | Planejado | — |

## Build Windows x64 homologado localmente

```text
Arquivo: Technolife-RustDesk-Windows.exe
Plataforma: Windows 10/11 x64
Versão do configurador: 0.1.0-dev
RustDesk homologado: 1.4.9
Formato: self-contained, single-file, win-x64
```

O artefato local é gerado por:

```powershell
.\scripts\publish-windows.ps1
```

O script cria o executável e o checksum correspondente em
`artifacts/windows-x64/`. O SHA-256 do configurador fica no arquivo
`Technolife-RustDesk-Windows.exe.sha256` e não é fixado nesta documentação, pois um
novo build poderá produzir outro valor.

Esta homologação local validou ajuda, versão, detecção, configuração e idempotência do
comando `setup` no Windows de desenvolvimento. Windows Sandbox não está instalado
nesta máquina; portanto, o teste em uma máquina Windows limpa continua pendente.

O executável ainda não possui assinatura Authenticode e poderá apresentar aviso do
SmartScreen. Nenhuma tag, GitHub Release ou link público foi criado nesta etapa.

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
