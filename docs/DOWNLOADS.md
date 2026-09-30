# Downloads

Esta página será o índice oficial de builds homologados do Technolife RustDesk Configurator.

## Builds publicados

Ainda não existem builds estáveis publicados.

| Plataforma | Arquitetura | Arquivo | Versão | Status | Download |
|---|---|---|---|---|---|
| Windows 10/11 | x64 | `Technolife-RustDesk-Windows.exe` | `v0.1.0-beta.1` | ⛔ Referência técnica — não usar com cliente final | [Release](https://github.com/jotaCorsino/technolife-rustdesk/releases/tag/v0.1.0-beta.1) |
| Windows 10/11 | x64 | `Technolife-RustDesk-Windows.exe` | `v0.1.0-beta.2` | 🟡 Em desenvolvimento — RD-007.1 | — |
| Linux Debian/Ubuntu | x64 | `technolife-rustdesk-linux` | — | Planejado | — |
| macOS | Intel x64 | `Technolife-RustDesk-macOS-x64` | — | Planejado | — |
| macOS | Apple Silicon arm64 | `Technolife-RustDesk-macOS-arm64` | — | Planejado | — |

## Beta Windows x64 — v0.1.0-beta.1 reprovada para cliente final

> **Não distribuir a v0.1.0-beta.1 para clientes finais.** O motor técnico funciona, mas o executável publicado ainda depende do modelo de CLI: ao abrir por duplo clique sem argumentos, mostra ajuda em terminal e encerra. A correção está sendo tratada na RD-007.1 e será publicada como `v0.1.0-beta.2`.

### Registro técnico da Beta 1

```text
Arquivo: Technolife-RustDesk-Windows.exe
Plataforma: Windows 10/11 x64
Versão: v0.1.0-beta.1
RustDesk homologado: 1.4.9
Status: Referência técnica / não recomendada para cliente final
Formato: self-contained, single-file, win-x64
SHA-256: CDDDFF73B94975E88B04B4B4452A2375D76630B893D0B70C13E90D53AEFB6E45
```

- [Baixar EXE](https://github.com/jotaCorsino/technolife-rustdesk/releases/download/v0.1.0-beta.1/Technolife-RustDesk-Windows.exe)
- [Baixar checksum SHA-256](https://github.com/jotaCorsino/technolife-rustdesk/releases/download/v0.1.0-beta.1/Technolife-RustDesk-Windows.exe.sha256)
- [Abrir GitHub Release](https://github.com/jotaCorsino/technolife-rustdesk/releases/tag/v0.1.0-beta.1)

O artefato local é gerado por:

```powershell
.\scripts\publish-windows.ps1
```

O script cria o executável e o checksum correspondente em
`artifacts/windows-x64/`. Como esta release é imutável, o SHA-256 correspondente
está registrado nesta página.

Esta Beta validou ajuda, versão, detecção, configuração e idempotência do comando
`setup` no Windows de desenvolvimento. Entretanto, o primeiro teste do fluxo real por duplo clique mostrou que a experiência de uso não atende ao requisito do cliente leigo.

A próxima Beta somente será indicada para teste em campo quando o executável puder ser aberto normalmente, sem terminal ou parâmetros, executar o setup completo e apresentar uma interface gráfica mínima com progresso e resultado.

Windows Sandbox não está instalado nesta máquina; portanto, o teste em uma máquina Windows limpa continua pendente.

O executável ainda não possui assinatura Authenticode e poderá apresentar aviso do
SmartScreen.

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
