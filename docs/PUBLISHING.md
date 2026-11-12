# Publicación del paquete `ManagementLoggingClient`

Este documento describe cómo versionar y publicar el paquete en NuGet mediante el workflow de GitHub Actions [`publish.yml`](../.github/workflows/publish.yml) usando **Trusted Publishing** (sin API keys de larga duración).

---

## 1. Versionado semántico (SemVer)

| Tipo | Cuándo usarlo | Ejemplo |
|------|---------------|---------|
| **MAJOR** | Cambios incompatibles en la API pública | `2.0.0` → `3.0.0` |
| **MINOR** | Nuevas funcionalidades compatibles | `2.0.0` → `2.1.0` |
| **PATCH** | Correcciones sin cambios de API | `2.0.0` → `2.0.1` |
| **Pre-release** | Versiones de prueba | `2.1.0-beta.1` |

> [!IMPORTANT]
> NuGet **no permite volver a publicar una versión existente** (ni siquiera después de eliminarla de la lista). Cada publicación necesita un número nuevo, mayor que el último publicado.

## 2. La versión la define el tag

El workflow toma la versión **del nombre del tag** (quitando la `v` inicial) y la pasa a `dotnet pack` con `-p:Version=...`. No hace falta editar el `.csproj` para publicar, aunque se recomienda mantener `<Version>` sincronizado para compilaciones locales.

| Tag | Versión publicada |
|-----|-------------------|
| `v2.0.1` | `2.0.1` |
| `v2.1.0` | `2.1.0` |
| `v3.0.0-beta.1` | `3.0.0-beta.1` (pre-release) |

## 3. Publicar una versión nueva

```bash
# 1. (Opcional) actualizar <Version> en LoggingClient.csproj y hacer commit
git add LoggingClient.csproj
git commit -m "chore: bump version to 2.0.1"
git push origin main

# 2. Crear y subir el tag
git tag v2.0.1
git push origin v2.0.1
```

Al subir el tag, GitHub Actions ejecuta:

1. `dotnet restore` / `dotnet build -c Release`
2. `dotnet pack -c Release -p:Version=<versión del tag> -o ./artifacts`
3. `NuGet/login@v1` → intercambia el token OIDC por una API key temporal (1 hora)
4. `dotnet nuget push ./artifacts/*.nupkg --skip-duplicate` (si la versión ya existe en NuGet, no falla)
5. Crea un **GitHub Release** con el `.nupkg` adjunto y notas generadas automáticamente (pre-release si la versión tiene sufijo, p.ej. `-beta.1`)

### Crear un Release manualmente (sin pipeline)

```powershell
dotnet pack -c Release -p:Version=2.0.1 -o ./artifacts
gh release create v2.0.1 ./artifacts/ManagementLoggingClient.2.0.1.nupkg `
  --title "ManagementLoggingClient 2.0.1" --generate-notes
```

## 4. Configuración inicial (una sola vez)

### En nuget.org → *usuario* → **Trusted Publishing**
| Campo | Valor |
|-------|-------|
| Repository Owner | dueño del repo en GitHub |
| Repository | `LoggingClient` |
| Workflow File | `publish.yml` |
| Environment | *(vacío)* |

### En GitHub → *Settings* → *Secrets and variables* → *Actions*
| Secreto | Valor |
|---------|-------|
| `NUGET_USER` | Nombre de perfil de nuget.org (**no** el email) |

> [!NOTE]
> En repositorios privados la política queda activa temporalmente 7 días; se activa de forma permanente tras la primera publicación exitosa.

## 5. Verificación

1. **GitHub → Actions**: el job `publish` debe terminar en verde.
2. **nuget.org → Manage Packages**: la nueva versión aparece (la indexación puede tardar unos minutos).

## 6. Problemas comunes

| Error | Causa | Solución |
|-------|-------|----------|
| `409 Conflict` / *package already exists* | La versión ya está publicada | Crear un tag con versión superior |
| `At least one valid glob pattern or package must be specified` | No se generó el `.nupkg` | Revisar el paso `pack` en los logs |
| Falla `NuGet/login` | Política mal configurada o falta `NUGET_USER` | Revisar owner/repo/workflow en nuget.org y el secreto |

### Borrar un tag creado por error
```bash
git tag -d v2.0.1
git push origin --delete v2.0.1
```

## 7. Probar el paquete localmente antes de publicar

```bash
dotnet pack -c Release -p:Version=2.0.1 -o ./artifacts
```

En el proyecto consumidor:
```bash
dotnet add package ManagementLoggingClient --version 2.0.1 --source C:\Users\Usuario\Documents\GitHub\LoggingClient\artifacts
```
