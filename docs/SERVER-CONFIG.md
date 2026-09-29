# Configuração do servidor Technolife

Este documento registra os valores destinados à configuração dos clientes RustDesk da Technolife.

## Endpoints

```text
ID Server: remoto.technolife.net.br
Relay Server: remoto.technolife.net.br
```

## Chave pública

```text
mFwxUtIrUDZkAI3zvh4RrvJIMlipFye+ILMbwtdK8jM=
```

Esta é a chave pública fornecida para configuração dos clientes. Ela não deve ser confundida com chave privada do servidor, senha administrativa ou licença.

## String exportada

String fornecida pelo recurso de exportação da configuração do RustDesk:

```text
9JSPNpGOLRGd3JWTMl0KllnRwlGbNlkS2JnU0gmd6NTSBtmWEVlcJRXV4dnRtJiOikXZrJCLiIiOikGchJCLiInYuQXZu5SZmlGbv5GajVGdu8Gdv1WZyJiOikXYsVmciwiIyJmL0VmbuUmZpx2buh2YlRnLvR3btVmciojI0N3boJye
```

O espaço/entidade HTML `&#x20;` apresentado junto ao valor original não faz parte da string e não deve ser incluído.

## Método preferencial

O projeto deverá primeiro validar a configuração usando o mecanismo suportado pelo RustDesk:

```text
rustdesk --config "<CONFIG_STRING>"
```

No Windows:

```text
rustdesk.exe --config "<CONFIG_STRING>"
```

## Validação obrigatória antes da release

Antes de tratar essa string como configuração de produção:

1. usar uma instalação de teste do RustDesk;
2. aplicar a string;
3. confirmar visualmente os campos de servidor;
4. confirmar conexão ao servidor Technolife;
5. registrar versão do RustDesk usada no teste;
6. repetir o teste nas plataformas suportadas.

## Segurança

Não adicionar a este arquivo:

- senha permanente;
- token administrativo;
- credencial de usuário;
- chave privada;
- segredo de API;
- credencial SSH.

Caso a infraestrutura mude, atualizar este documento e os testes/configurações do projeto no mesmo conjunto de alterações.
