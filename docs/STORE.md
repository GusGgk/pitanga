# Publicar na Microsoft Store

A Store **assina o pacote por você**, então quem instala por lá não vê o aviso do SmartScreen.
Tudo que é técnico já está pronto no repositório (`packaging/msix/`); faltam só os passos que dependem
da sua conta.

## 1. Conta de desenvolvedor (uma vez)

1. Acesse o **Partner Center** (<https://partner.microsoft.com/dashboard>) e cadastre-se no programa
   de apps do Windows como **desenvolvedor individual** (a Microsoft tornou o cadastro de pessoa física
   gratuito — confira na tela de cadastro se ainda é o caso).
2. Em **Partner Center → Apps and games → New product → MSIX or PWA app**, reserve o nome **Pitanga**.
3. Abra o app → **Product management → Product identity** e anote:
   - `Package/Identity/Name` (ex.: `12345GusGk.Pitanga`)
   - `Package/Identity/Publisher` (ex.: `CN=ABCD1234-...`)
   - `Package/Properties/PublisherDisplayName` (ex.: `GusGk`)

## 2. Gerar o pacote

### Opção A — automático (recomendado)

No GitHub, em **Settings → Secrets and variables → Actions → Variables**, crie:

| Variável | Valor |
|---|---|
| `MSIX_IDENTITY_NAME` | o `Package/Identity/Name` |
| `MSIX_PUBLISHER` | o `Package/Identity/Publisher` |
| `MSIX_PUBLISHER_DISPLAY_NAME` | o `PublisherDisplayName` |

A partir daí, cada tag `v*` também gera `Pitanga-<versão>-x64.msix` na release.

### Opção B — no seu PC

Precisa do .NET 10 SDK e do [Windows SDK](https://developer.microsoft.com/windows/downloads/windows-sdk/):

```powershell
.\packaging\msix\build-msix.ps1 -Version 1.0.0 -IdentityName "<Name>" -Publisher "<CN=...>" -PublisherDisplayName "<nome>"
```

## 3. Enviar

Em **Submissions → Packages**, envie o `.msix`. Na certificação, o Pitanga declara a capacidade
restrita **`runFullTrust`** e usa hooks globais de teclado/mouse. Explique no campo
*Notes for certification* (pode copiar):

> Pitanga is a file-conversion utility. It uses a low-level mouse/keyboard hook only to detect the gesture
> "drag a file and tap Shift", which opens a radial menu next to the cursor. Keystrokes are never recorded,
> stored or transmitted, and no key is ever blocked. The app makes no network connections. Conversions of
> Office documents use the user's installed Microsoft Office through COM automation. Privacy policy:
> https://github.com/GusGgk/pitanga/blob/main/PRIVACY.md

A Store pede também uma **URL de política de privacidade**: use o link do `PRIVACY.md` no GitHub.

## Diferenças da versão da Store

- "Start with Windows" usa a tarefa de inicialização do pacote (`PitangaStartup`), controlável em
  *Configurações → Apps → Inicialização*. O código detecta sozinho quando roda empacotado.
- As atualizações vêm pela Store; o instalador `.exe` do GitHub continua existindo em paralelo.
