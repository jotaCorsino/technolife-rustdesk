# Downloads

Esta página será o índice oficial de builds homologados do Technolife RustDesk Configurator.

## Builds estáveis

Ainda não existem builds estáveis publicados.

| Plataforma | Arquitetura | Arquivo planejado | Versão | Download | SHA-256 |
|---|---|---|---|---|---|
| Windows | x64 | `Technolife-RustDesk-Windows.exe` | — | — | — |
| Linux Debian/Ubuntu | x64 | `technolife-rustdesk-linux` | — | — | — |
| macOS | Intel x64 | `Technolife-RustDesk-macOS-x64` | — | — | — |
| macOS | Apple Silicon arm64 | `Technolife-RustDesk-macOS-arm64` | — | — | — |

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
