# SPID / CIE OIDC — Relying Party .NET (test locale)

[![License: Apache 2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](./LICENSE)
[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![OpenID Connect Federation 1.0](https://img.shields.io/badge/OIDC-Federation_1.0-F7931E.svg)](https://openid.net/specs/openid-federation-1_0.html)

Relying Party **ASP.NET Core 9 MVC** (C#) conforme a **OpenID Connect Federation 1.0**
(profilo SPID/CIE italiano), testabile **interamente in locale** contro l'infrastruttura
ufficiale AgID (`italia/spid-cie-oidc-django`) eseguita in Docker.

Architettura applicativa: **MVC** (Controller + Razor Views), niente interfacce, niente
unit test. Le **chiavi private** del RP sono **fuori dal repo** e caricate da un secret
provider (user-secrets / env / Key Vault / file montato) — vedi sezione *Secret*.

Il flusso completo è stato verificato end-to-end: login reale → claim utente
(`nome`, `cognome`, `email`, `codice fiscale`) restituiti dall'endpoint UserInfo cifrato.

---

## Architettura

```
┌──────────────────────┐   trust chain    ┌──────────────────────────┐
│  trust-anchor.org     │◄────────────────►│  cie-provider.org         │
│  :8000 Trust Anchor   │                  │  :8002  OP CIE            │
│       + OP SPID       │                  │  (italia docker img)      │
│  (italia docker img)  │                  │                           │
└──────────┬───────────┘                  └────────────┬─────────────┘
           │ subordinate stmt                          │ authz / token / userinfo
           │                                            │
           ▼                                            ▼
        ┌────────────────────────────────────────────────────┐
        │  relying-party.org:8001  →  QUESTO PROGETTO (.NET)   │
        │  - entity configuration firmata                      │
        │  - trust chain resolver                              │
        │  - request object (JAR) firmato + PKCE               │
        │  - token endpoint via private_key_jwt                │
        │  - UserInfo JWE (RSA-OAEP) → JWS verify              │
        └────────────────────────────────────────────────────┘
```

Il RP .NET **sostituisce** il RP Django dei demo ufficiali: riusa le stesse chiavi e
metadati (parte pubblica in `src/SpidCieOidc.Web/rp_public.json`, chiavi private in
`secrets/`, estratte dalle fixture `italia/spid-cie-oidc-django`), che il Trust Anchor già
registra come discendente. Per questo è considerato fidato senza onboarding manuale.

---

## Prerequisiti

- **Docker** + Docker Compose (testato 28.x)
- **.NET SDK 9** (solo per build/sviluppo fuori da Docker; in Docker non serve)
- Permessi di amministratore per modificare il file `hosts` (una volta sola)

---

## Avvio rapido

### 0. Abilita l'esecuzione degli script PowerShell (una volta sola)

Di default Windows blocca i file `.ps1` (`L'esecuzione di script è disabilitata`).
Abilita gli script **solo per il tuo utente** (non serve admin, scelta sicura):

```powershell
Set-ExecutionPolicy -Scope CurrentUser RemoteSigned
```

`RemoteSigned` = gli script locali girano, quelli scaricati da internet richiedono firma.
Verifica con `Get-ExecutionPolicy -List`.

> Alternative senza modificare il sistema:
> - una tantum: `powershell -ExecutionPolicy Bypass -File .\scripts\start.ps1`
> - salta gli script ed usa direttamente `docker compose up --build`.

### 1. Hostname locali (una volta sola, come Amministratore)

I container e il browser devono risolvere gli stessi hostname. PowerShell **come admin**:

```powershell
./scripts/add-hosts.ps1
```

Aggiunge a `C:\Windows\System32\drivers\etc\hosts`:

```
127.0.0.1   trust-anchor.org relying-party.org cie-provider.org
```

### 2. Chiavi private del RP (una volta sola)

Le chiavi non sono nel repo. Per la demo, crea il file secret (chiavi di test) dal sample:

```powershell
./scripts/setup-secrets.ps1
```

Crea `secrets/rp_private_keys.json` (gitignored), montato nel container RP via
`docker-compose.yml`. Per la produzione vedi la sezione **Secret**.

### 3. Avvio dell'intera federazione

```powershell
./scripts/start.ps1
# oppure:  docker compose up --build
```

Attendi che i tre servizi siano pronti (il primo avvio scarica l'immagine italia e
compila il RP .NET). Trust Anchor e OP eseguono `migrate` + `loaddata` a ogni avvio.

### 4. Login

Apri il browser su:

```
http://relying-party.org:8001/oidc/rp/landing
```

La landing usa i **componenti grafici ufficiali**: il pulsante AgID
[`spid-sp-access-button`](https://github.com/italia/spid-sp-access-button) per **Entra con
SPID** (menu IdP, ordine randomizzato come da regole AgID) e
[`cie-graphics`](https://github.com/italia/cie-graphics) per **Entra con CIE** (vendored in
`wwwroot/lib/`, vedi `wwwroot/lib/README.md`). I provider sono configurabili:

- `Oidc:ProductionProviders` (`appsettings.json`) — IdP/OP di **produzione**, mostrati
  **sempre**. `EntityId` = entity id di federazione di ogni provider (i valori inclusi sono
  segnaposto da sostituire con quelli reali prima del go-live).
- `Oidc:LocalProviders` (`appsettings.Development.json`) — provider **locali di test**,
  mostrati **solo in `Development`** (badge "locale"), in aggiunta a quelli di produzione.

In `Development` (lo stack Docker imposta `ASPNETCORE_ENVIRONMENT=Development`) vedi anche i
bottoni locali → OP SPID su `trust-anchor.org:8000/oidc/op` e OP CIE su
`cie-provider.org:8002/oidc/op`. In `Production` compaiono **solo** i provider reali.

Ogni voce avvia `GET /oidc/rp/authorization?profile=<spid|cie>&provider=<entityId>`.

> ⚠️ I provider di **produzione** funzionano solo se il RP è realmente registrato nella
> federazione SPID/CIE di produzione (chiavi e trust anchor reali). Con le chiavi demo
> falliscono la trust chain: in locale usa i bottoni **locali**.

Clicca un provider **locale** e usa le credenziali demo:

| Username | Password   |
|----------|------------|
| `admin`  | `oidcadmin`|
| `user`   | `oidcuser` |

Al termine vedrai nome, cognome, email, codice fiscale e i claim grezzi.

---

## Flusso implementato (SPID/CIE OIDC)

1. **Entity Configuration** — `GET /.well-known/openid-federation`: JWT firmato con la
   chiave di federazione (`typ: entity-statement+jwt`), con metadata `openid_relying_party`,
   `authority_hints` e `trust_marks`.
2. **Trust chain** — risoluzione del provider: entity config OP (self-signed) → subordinate
   statement del Trust Anchor sull'OP → entity config del Trust Anchor.
3. **Authorization Request** — request object firmato (JAR) con PKCE S256, `nonce`, `state`,
   `acr_values`, `claims` per profilo, `prompt=consent login`; il client_id è l'entity id del RP.
4. **Automatic client registration** — l'OP recupera l'entity config del RP e ne risolve la
   trust chain (nessuna registrazione statica).
5. **Token Request** — `authorization_code` + `code_verifier`, autenticazione client
   **`private_key_jwt`** (client_assertion firmata con la core sig key).
6. **id_token** — verifica firma (JWKS OP), `iss`, `aud`, `nonce`.
7. **UserInfo** — risposta **JWE** (`RSA-OAEP` / `A128CBC-HS256`) decifrata con la core enc
   key del RP, poi verifica del JWS interno firmato dall'OP.
8. **Refresh token** — `GET /oidc/rp/refresh`: `grant_type=refresh_token` + `private_key_jwt`,
   rinnova access/refresh token.
9. **Logout (RP-initiated)** — `GET /oidc/rp/logout`: revoca l'access token al
   `revocation_endpoint` dell'OP (`private_key_jwt`) ed elimina la sessione locale.

> **Refresh token e acr.** L'OP rilascia un `refresh_token` solo con
> `scope=offline_access` + `prompt=consent` + un acr **SpidL1**. Per questo il RP invia
> `acr_values` come **lista** `["…/SpidL2","…/SpidL1"]` e `scope="openid offline_access"`.
> Endpoint utente: `landing` → `authorization` → `callback` → `refresh` / `logout`.

---

## Mappa dei file (`src/SpidCieOidc.Web`)

| Percorso | Ruolo |
|----------|-------|
| `Program.cs` | bootstrap, DI, MVC, forwarded headers, sorgenti config/secret |
| `rp_public.json` | identità RP **pubblica**: sub, authority_hints, metadata (JWKS pubbliche), trust marks |
| `Controllers/FederationController.cs` | `/.well-known/openid-federation`, `jwks.json`, `jwks.jose` |
| `Controllers/HomeController.cs` | `/`, `/oidc/rp/landing`, `/error` |
| `Controllers/AuthController.cs` | `/oidc/rp/authorization` · `callback` · `refresh` · `logout` |
| `Views/` | Razor: `Home/Index`, `Auth/Profile`, `Auth/LoggedOut`, `Shared/Error`, `_Layout` |
| `wwwroot/css/site.css` | stile |
| `Services/RpConfig.cs` | carica config pubblica + **chiavi private dal secret provider** |
| `Services/Jwk.cs` | modello JWK ↔ RSA (CRT derivati da p,q,d; padding lunghezze) |
| `Services/JwtService.cs` | JWS sign/verify (RS256), JWE decrypt (jose-jwt) |
| `Services/EntityConfigurationBuilder.cs` | costruzione/firma entity configuration |
| `Services/TrustChainResolver.cs` | risoluzione e validazione trust chain dell'OP |
| `Services/OidcClient.cs` | authorization, token, userinfo, refresh, logout (cuore del flusso) |
| `Services/AuthSession.cs` | store in-memory delle sessioni (state→sessione) |
| `secrets/` (repo root) | chiavi private RP: `*.sample.json` committato, `rp_private_keys.json` gitignored |
| `infra/federation_authority`, `infra/provider` | dump/config Trust Anchor e OP (immagine italia) |

---

## Secret (chiavi private del RP)

Le chiavi private (federation + core) **non sono mai nel repo né nell'immagine**.
`RpConfig.ResolvePrivateKeys` le cerca in quest'ordine:

1. **`Rp:PrivateKeysFile`** — percorso a un file secret **montato**. Usato sia in Docker
   (`docker-compose.yml` monta `./secrets` + `Rp__PrivateKeysFile=/secrets/rp_private_keys.json`)
   sia in `dotnet run` (`appsettings.Development.json`). K8s: monta un Secret allo stesso path.
2. **`Rp:PrivateKeys`** — JSON inline da variabile d'ambiente `Rp__PrivateKeys` o da un
   secret store gestito (es. **Azure Key Vault**, snippet commentato in `Program.cs`).

Niente `user-secrets`. Se nessuna fonte è presente l'app fallisce all'avvio con messaggio
esplicito. Formato: `{ "jwks_fed": { "keys": [...] }, "jwks_core": { "keys": [...] } }`.

> Le chiavi `*.sample.json` sono **materiale di test pubblico** (fixture italia): usale
> solo per la demo, mai in produzione.

---

## Test automatico end-to-end

Lo script `e2e_test.sh` guida l'intero flusso senza browser (login `admin/oidcadmin`,
consenso, callback, refresh, logout) e stampa i claim ottenuti. Richiede lo stack avviato:

```bash
bash e2e_test.sh cie     # profilo CIE
bash e2e_test.sh spid    # profilo SPID
```

Output atteso: `Autenticazione riuscita`, `Codice fiscale = TINIT-…`,
`Token aggiornati` (refresh), `Logout effettuato`.

---

## Sviluppo (RP fuori da Docker)

```powershell
./scripts/setup-secrets.ps1                # crea il file chiavi (se manca)
dotnet run --project src/SpidCieOidc.Web   # legge Rp:PrivateKeysFile da appsettings.Development.json
```

⚠️ In questa modalità l'OP (in Docker) **non** riesce a raggiungere il RP su
`relying-party.org:8001` per la registrazione automatica. Per il flusso completo usa la
modalità Docker. Lo sviluppo standalone è utile per testare entity config, firma JWT, ecc.

---

## Troubleshooting

| Sintomo | Causa / rimedio |
|---------|-----------------|
| `L'esecuzione di script è disabilitata` | ExecutionPolicy → `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned` (vedi step 0) |
| Browser non risolve `relying-party.org` | `hosts` non aggiornato → riesegui `add-hosts.ps1` come admin |
| `Trust chain validation failed` | TA/OP non ancora pronti (migrate/loaddata) → attendi qualche secondo |
| `Token endpoint 4xx` | clock disallineato (JAR exp 60s) o container OP non pronto |
| Porte 8000/8001/8002 occupate | libera le porte o cambia mapping in `docker-compose.yml` |

---

## Licenza

Il codice del Relying Party .NET sviluppato in questo repository (`src/`, `scripts/`,
`docker-compose.yml`, documentazione) è rilasciato sotto **Apache License 2.0** —
vedi [`LICENSE`](./LICENSE). Copyright © 2026 Filippo D'Errigo.

Il repository **include componenti di terze parti** con licenze proprie, elencate in
[`NOTICE`](./NOTICE): la licenza Apache-2.0 del progetto **non** si estende a questi.
In sintesi:

| Componente | Origine | Licenza |
|------------|---------|---------|
| Infrastruttura di test (`infra/`, chiavi demo) | [`italia/spid-cie-oidc-django`](https://github.com/italia/spid-cie-oidc-django) | Apache-2.0 ([`infra/UPSTREAM_LICENSE`](infra/UPSTREAM_LICENSE)) |
| Bottone SPID (`wwwroot/lib/spid-sp-access-button/`) | [`italia/spid-sp-access-button`](https://github.com/italia/spid-sp-access-button) | OFL-1.1 / MIT (vedi `LICENSE.md` nella cartella) |
| Grafica CIE (`wwwroot/lib/cie-graphics/`) | [`italia/cie-graphics`](https://github.com/italia/cie-graphics) | asset ufficiali PA |
| Dipendenze NuGet (es. `jose-jwt`) | NuGet (a build time) | MIT e altre |

> Le chiavi `secrets/*.sample.json` sono **materiale di test pubblico** delle fixture
> upstream: solo per la demo locale, **mai in produzione**. Le chiavi private reali non
> sono né nel repo né nell'immagine (vedi sezione **Secret**).

I loghi e i marchi SPID/CIE e degli Identity Provider sono dei rispettivi proprietari.

## Crediti

Infrastruttura di test (Trust Anchor, OpenID Provider, chiavi demo) da
[`italia/spid-cie-oidc-django`](https://github.com/italia/spid-cie-oidc-django)
(Apache-2.0, vedi `infra/UPSTREAM_LICENSE`). La config vendored è documentata e pinnata in
[`infra/README.md`](infra/README.md); l'immagine Docker è **pinnata per digest** in
`docker-compose.yml` per la riproducibilità. Questo progetto implementa il Relying Party
in .NET/C#.
