# Assinatura de código (remover o aviso do SmartScreen)

Sem assinatura, ao abrir o `Pitanga-Setup.exe` baixado do GitHub o Windows mostra
**"O Windows protegeu o computador"** (é preciso clicar em *Mais informações → Executar assim mesmo*).
Isso não é um erro do app: é o que acontece com qualquer `.exe` novo sem assinatura digital.

Há três caminhos — do mais simples ao mais "profissional":

## 1. Microsoft Store (grátis, recomendado para começar)

A Store assina o pacote. Veja [STORE.md](STORE.md). Usuários da Store não veem aviso algum.

## 2. SignPath Foundation (grátis para open source)

A [SignPath Foundation](https://signpath.org) assina projetos open source gratuitamente com um
certificado deles, direto da CI do GitHub.

1. Publique o repositório (público, licença MIT — já está pronto).
2. Faça o pedido pelo formulário no site da SignPath Foundation, com o link do repositório.
3. Aprovado, eles fornecem a configuração; o passo de assinatura entra no
   `.github/workflows/release.yml`, entre o *publish* e o *Inno Setup* (o `.exe` e o instalador são
   enviados para assinatura e voltam assinados).

## 3. Certificado próprio (pago)

- **Azure Trusted Signing / Artifact Signing** (~US$ 10/mês): confira se a validação de pessoa física
  está disponível no Brasil no momento do cadastro.
- **Certificado OV/EV** de uma autoridade (Sectigo, DigiCert, Certum…): anual, costuma exigir token USB.

Com qualquer certificado, a assinatura é um comando `signtool sign /fd SHA256 /tr <timestamp> ...`
aplicado ao `Pitanga.exe` (antes do Inno Setup) e ao `Pitanga-Setup-*.exe` (depois).

> Mesmo assinado, um certificado novo leva um tempo para ganhar "reputação" no SmartScreen.
> EV e Trusted Signing ganham reputação mais rápido.
