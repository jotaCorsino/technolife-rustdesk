# Segurança

## Princípio

O configurador deve possuir apenas os dados necessários para apontar o RustDesk ao servidor Technolife. Ele não deve transformar o pacote de instalação em um repositório de credenciais.

## Permitido no código/configuração distribuída

- hostname público do ID Server;
- hostname público do Relay Server;
- chave pública do RustDesk;
- string exportada de configuração, após validação de que contém apenas configuração distribuível;
- versão homologada do RustDesk;
- URL pública de download;
- checksum de pacote.

## Proibido no repositório

- chaves privadas;
- senhas permanentes de acesso remoto;
- senhas administrativas;
- tokens de API administrativos;
- credenciais SSH;
- cookies/sessões;
- credenciais de clientes;
- segredos de CI.

## Downloads

Todo download automático deverá:

1. usar origem oficial ou explicitamente homologada;
2. usar HTTPS;
3. validar checksum antes de executar;
4. falhar de forma segura em caso de divergência;
5. registrar versão e resultado da validação.

## Execução elevada

O programa não deve assumir que está sendo executado como administrador/root.

Ele deve:

- detectar quando elevação é necessária;
- solicitar apenas quando a operação exigir;
- informar claramente falhas de permissão;
- evitar executar toda a aplicação elevada quando uma operação pontual for suficiente.

## Processos externos

- não concatenar argumentos não confiáveis em comandos de shell;
- preferir APIs de argumentos estruturados;
- capturar exit code;
- aplicar timeout quando fizer sentido;
- registrar stderr sem incluir segredos.

## Logs

Logs podem conter diagnóstico técnico, mas não dados secretos.

Quando uma string potencialmente sensível for passada a um processo, o log deve registrar algo como:

```text
Aplicando configuração RustDesk...
```

e não reproduzir o valor completo.

## Assinatura e confiança

A assinatura de código não é requisito da primeira prova de conceito, mas deve ser considerada antes de distribuição ampla.

Futuro:

- assinatura Authenticode para Windows;
- assinatura/notarização para macOS;
- checksums publicados nas releases.

## Mudanças de infraestrutura

Se a chave pública ou endpoints mudarem:

- criar alteração explícita;
- revisar impacto;
- testar contra o servidor;
- atualizar documentação;
- gerar nova versão do configurador.
