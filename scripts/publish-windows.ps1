[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$solutionPath = Join-Path $repositoryRoot 'Technolife.RustDesk.sln'
$projectPath = Join-Path $repositoryRoot 'src\Technolife.RustDesk.Windows\Technolife.RustDesk.Windows.csproj'
$artifactRoot = Join-Path $repositoryRoot 'artifacts'
$artifactDirectory = Join-Path $artifactRoot 'windows-x64'
$publishDirectory = Join-Path $repositoryRoot 'src\Technolife.RustDesk.Windows\obj\publish\windows-x64'
$executableName = 'Technolife-RustDesk-Windows.exe'
$publishedExecutable = Join-Path $publishDirectory $executableName
$artifactExecutable = Join-Path $artifactDirectory $executableName
$checksumPath = "$artifactExecutable.sha256"

function Assert-RepositoryChildPath {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $fullPath = [IO.Path]::GetFullPath($Path)
    $expectedPrefix = $repositoryRoot.TrimEnd('\') + '\'

    if (-not $fullPath.StartsWith($expectedPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Caminho fora do repositório recusado: $fullPath"
    }
}

function Invoke-DotNet {
    param(
        [Parameter(Mandatory)]
        [string[]] $Arguments
    )

    & dotnet @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') falhou com código $LASTEXITCODE."
    }
}

Assert-RepositoryChildPath -Path $artifactDirectory
Assert-RepositoryChildPath -Path $publishDirectory

Set-Location -LiteralPath $repositoryRoot

if (Test-Path -LiteralPath $artifactDirectory) {
    Remove-Item -LiteralPath $artifactDirectory -Recurse -Force
}

if (Test-Path -LiteralPath $publishDirectory) {
    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $publishDirectory | Out-Null

Write-Host 'Restaurando dependências...'
Invoke-DotNet -Arguments @('restore', $solutionPath)
Invoke-DotNet -Arguments @('restore', $projectPath, '--runtime', 'win-x64')

Write-Host 'Compilando em Release...'
Invoke-DotNet -Arguments @('build', $solutionPath, '--configuration', 'Release', '--no-restore')

Write-Host 'Executando testes em Release...'
Invoke-DotNet -Arguments @('test', $solutionPath, '--configuration', 'Release', '--no-build', '--no-restore')

Write-Host 'Limpando binários Release antes do empacotamento final...'
Invoke-DotNet -Arguments @(
    'clean',
    $solutionPath,
    '--configuration', 'Release',
    '--verbosity', 'quiet'
)

Write-Host 'Publicando Windows x64 self-contained e single-file...'
Invoke-DotNet -Arguments @(
    'publish',
    $projectPath,
    '--configuration', 'Release',
    '--runtime', 'win-x64',
    '--self-contained', 'true',
    '--no-restore',
    '--property:DebugType=None',
    '--property:DebugSymbols=false',
    '--output', $publishDirectory
)

if (-not (Test-Path -LiteralPath $publishedExecutable -PathType Leaf)) {
    throw "Executável publicado não encontrado: $publishedExecutable"
}

Copy-Item -LiteralPath $publishedExecutable -Destination $artifactExecutable

$hash = (Get-FileHash -LiteralPath $artifactExecutable -Algorithm SHA256).Hash
Set-Content `
    -LiteralPath $checksumPath `
    -Value "$hash  $executableName" `
    -Encoding Ascii

$size = (Get-Item -LiteralPath $artifactExecutable).Length

Write-Host ''
Write-Host 'Build Windows x64 concluído.'
Write-Host ''
Write-Host 'Arquivo:'
Write-Host $artifactExecutable
Write-Host ''
Write-Host 'Tamanho (bytes):'
Write-Host $size
Write-Host ''
Write-Host 'SHA-256:'
Write-Host $hash
Write-Host ''
Write-Host 'Testes:'
Write-Host 'concluídos com sucesso.'
