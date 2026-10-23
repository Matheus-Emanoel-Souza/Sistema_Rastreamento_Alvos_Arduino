<#
.SYNOPSIS
    Copia o genoma mais recentemente exportado da tela "Treinamento do Controlador" para dentro
    do projeto, para ser embutido no executável como o controlador de fábrica.

.DESCRIPTION
    Chamado pelo target MSBuild "EmbutirGenomaTreinado" (ver RadarTorres.App.csproj) antes de
    cada build. Procura em %AppData%\RadarTorres\Treinamento os arquivos
    "genoma-geracao-*.json" exportados pela tela de treinamento (ver
    TreinamentoControladorViewModel.ExportarMelhorGenoma) e copia o mais recente (maior
    MelhorFitness; em empate, o mais recentemente escrito em disco) para
    Resources\ControladorTreinado\genoma-controlador.json.

    Se a pasta de origem não existir ou não tiver nenhum arquivo, não faz nada — o projeto
    continua com o genoma já commitado (de fábrica, zerado = equivalente à heurística pura, ou
    o último que alguém já tiver embutido antes).
#>
param(
    [Parameter(Mandatory = $true)][string]$PastaOrigem,
    [Parameter(Mandatory = $true)][string]$Destino
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $PastaOrigem)) {
    Write-Host "EmbutirGenomaTreinado: pasta '$PastaOrigem' não existe — nenhum genoma foi exportado ainda nesta máquina. Mantendo o genoma já embutido."
    exit 0
}

$arquivos = Get-ChildItem -Path $PastaOrigem -Filter 'genoma-geracao-*.json' -File -ErrorAction SilentlyContinue
if (-not $arquivos -or $arquivos.Count -eq 0) {
    Write-Host "EmbutirGenomaTreinado: nenhum arquivo genoma-geracao-*.json em '$PastaOrigem'. Mantendo o genoma já embutido."
    exit 0
}

# Critério: maior MelhorFitness primeiro (é o que importa, não a ordem de exportação); em
# empate, o mais recente por data de escrita.
$melhor = $arquivos | ForEach-Object {
    try {
        $conteudo = Get-Content -Path $_.FullName -Raw | ConvertFrom-Json
        [PSCustomObject]@{
            Arquivo       = $_
            MelhorFitness = [double]$conteudo.MelhorFitness
        }
    } catch {
        Write-Host "EmbutirGenomaTreinado: ignorando '$($_.FullName)' (JSON inválido: $($_.Exception.Message))."
        $null
    }
} | Where-Object { $_ -ne $null } |
    Sort-Object -Property MelhorFitness, @{Expression = { $_.Arquivo.LastWriteTimeUtc }} -Descending |
    Select-Object -First 1

if (-not $melhor) {
    Write-Host "EmbutirGenomaTreinado: nenhum arquivo válido encontrado. Mantendo o genoma já embutido."
    exit 0
}

$pastaDestino = Split-Path -Path $Destino -Parent
if (-not (Test-Path $pastaDestino)) {
    New-Item -ItemType Directory -Path $pastaDestino -Force | Out-Null
}

Copy-Item -Path $melhor.Arquivo.FullName -Destination $Destino -Force
Write-Host "EmbutirGenomaTreinado: embutido '$($melhor.Arquivo.Name)' (melhor fitness = $($melhor.MelhorFitness))."
