#Requires -Version 5.1
<#
Installe le binaire natif de Xavier (agent candidat Cerebro) pour Windows.

    irm https://raw.githubusercontent.com/CODA-SCHOOL-FRANCE/cerebro/main/packaging/install.ps1 | iex

Variables d'environnement optionnelles :
  XAVIER_VERSION     version précise à installer (ex: 0.1.0) ; par défaut, la dernière release.
  XAVIER_INSTALL_DIR dossier d'installation ; par défaut %LOCALAPPDATA%\Xavier\bin.
#>

$ErrorActionPreference = "Stop"

$Repo = "CODA-SCHOOL-FRANCE/cerebro"
$InstallDir = if ($env:XAVIER_INSTALL_DIR) { $env:XAVIER_INSTALL_DIR } else { Join-Path $env:LOCALAPPDATA "Xavier\bin" }

function Fail($Message) {
    Write-Error $Message
    Write-Host "Installation manuelle : téléchargez l'archive correspondante sur https://github.com/$Repo/releases"
    exit 1
}

if (-not [Environment]::Is64BitOperatingSystem) {
    Fail "Windows 32 bits non supporté — seul win-x64 est publié."
}
$Rid = "win-x64"

try {
    # Un seul tag vX.Y.Z publie à la fois l'image Docker du serveur et les archives agent (voir
    # release.yml) : /releases/latest pointe donc toujours vers une release contenant les archives
    # recherchées ci-dessous, pas besoin de filtrer par nom de tag.
    if ($env:XAVIER_VERSION) {
        $Release = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases/tags/v$($env:XAVIER_VERSION)"
    } else {
        $Release = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases/latest"
    }
} catch {
    Fail "Impossible de récupérer la release ($($_.Exception.Message))."
}

$Asset = $Release.assets | Where-Object { $_.name -like "*-$Rid.zip" } | Select-Object -First 1
if (-not $Asset) {
    Fail "Aucune archive '*-$Rid.zip' trouvée dans la release $($Release.tag_name)."
}

Write-Host "Téléchargement de $($Asset.name) ($($Release.tag_name))..."

$TmpDir = Join-Path ([System.IO.Path]::GetTempPath()) ([System.Guid]::NewGuid())
New-Item -ItemType Directory -Path $TmpDir | Out-Null
try {
    $ZipPath = Join-Path $TmpDir "xavier.zip"
    Invoke-WebRequest -Uri $Asset.browser_download_url -OutFile $ZipPath

    $ExtractDir = Join-Path $TmpDir "extracted"
    Expand-Archive -Path $ZipPath -DestinationPath $ExtractDir

    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
    Copy-Item -Path (Join-Path $ExtractDir "xavier.exe") -Destination (Join-Path $InstallDir "xavier.exe") -Force

    # Ne jamais écraser un xavier.config.json déjà présent : le surveillant a pu le remplir avec
    # les vraies valeurs de la session (voir docs/DEPLOYMENT-AGENT.md) - un ré-lancement de ce
    # script (mise à jour de version, par exemple) ne doit pas silencieusement le réinitialiser. Le
    # fichier livré dans l'archive a des champs à null par défaut (docs/xavier.config.json) : tant
    # qu'il n'est pas édité, l'agent retombe sur les prompts interactifs exactement comme en son
    # absence.
    $ConfigDestination = Join-Path $InstallDir "xavier.config.json"
    if (-not (Test-Path $ConfigDestination)) {
        Copy-Item -Path (Join-Path $ExtractDir "xavier.config.json") -Destination $ConfigDestination
    }
} finally {
    Remove-Item -Recurse -Force $TmpDir -ErrorAction SilentlyContinue
}

Write-Host ""
Write-Host "Xavier installé dans $InstallDir\xavier.exe"

$UserPath = [Environment]::GetEnvironmentVariable("Path", "User")
if (-not ($UserPath -split ";" -contains $InstallDir)) {
    Write-Host ""
    Write-Host "$InstallDir n'est pas dans votre PATH. Pour l'ajouter :"
    Write-Host "  [Environment]::SetEnvironmentVariable('Path', `"`$env:Path;$InstallDir`", 'User')"
    Write-Host "  (puis rouvrir le terminal)"
}

Write-Host ""
Write-Host "Usage : xavier <serverUrl> <sessionCode> <candidateId> [certThumbprint]"
Write-Host "Un xavier.config.json a été déposé dans $InstallDir\ : si votre surveillant vous a"
Write-Host "communiqué les valeurs de la session, éditez-le pour ne plus avoir à les saisir à chaque"
Write-Host "lancement (sinon, laissez-le tel quel - xavier les redemandera simplement)."
