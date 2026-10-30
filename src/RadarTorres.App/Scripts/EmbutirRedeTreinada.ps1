<#
.SYNOPSIS
    Copia a rede mais recentemente exportada da tela "Treinamento do Controlador" para dentro
    do projeto, para ser embutida no executável como o controlador de fábrica.

.DESCRIPTION
    Chamado pelo target MSBuild "EmbutirRedeTreinada" (ver RadarTorres.App.csproj) antes de
    cada build. Procura em %AppData%\RadarTorres\Treinamento os arquivos "rede-geracao-*.net"
    exportados pela tela de treinamento (ver TreinamentoControladorViewModel.ExportarMelhorGenoma)
    e copia o mais avançado para Resources\ControladorTreinado\rede-controlador.net.

    Critério de escolha: MAIOR NÚMERO DE GERAÇÃO no nome do arquivo; em empate, o mais recente
    por data de escrita. Antes o critério era o maior MelhorFitness lido de dentro do JSON — o
    formato .net do SharpNEAT guarda só a topologia e os pesos da rede, sem metadado de fitness,
    então o número da geração passou a ser o melhor indicador disponível de "treinou mais".

    Se a pasta de origem não existir ou não tiver nenhum arquivo, não faz nada — o projeto
    continua com a rede já commitada (ou sem nenhuma, caso em que o app roda só com a
    heurística; ver ControladorTreinadoProvider).
#>
param(
    [Parameter(Mandatory = $true)][string]$PastaOrigem,
    [Parameter(Mandatory = $true)][string]$Destino
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $PastaOrigem)) {
    Write-Host "EmbutirRedeTreinada: pasta '$PastaOrigem' não existe — nenhuma rede foi exportada ainda nesta máquina. Mantendo a rede já embutida."
    exit 0
}

$arquivos = Get-ChildItem -Path $PastaOrigem -Filter 'rede-geracao-*.net' -File -ErrorAction SilentlyContinue
if (-not $arquivos -or $arquivos.Count -eq 0) {
    Write-Host "EmbutirRedeTreinada: nenhum arquivo rede-geracao-*.net em '$PastaOrigem'. Mantendo a rede já embutida."
    exit 0
}

$melhor = $arquivos | ForEach-Object {
    if ($_.BaseName -match '^rede-geracao-(\d+)$') {
        [PSCustomObject]@{
            Arquivo = $_
            Geracao = [int]$Matches[1]
        }
    } else {
        Write-Host "EmbutirRedeTreinada: ignorando '$($_.Name)' (nome fora do padrão rede-geracao-N.net)."
        $null
    }
} | Where-Object { $_ -ne $null } |
    Sort-Object -Property Geracao, @{Expression = { $_.Arquivo.LastWriteTimeUtc }} -Descending |
    Select-Object -First 1

if (-not $melhor) {
    Write-Host "EmbutirRedeTreinada: nenhum arquivo válido encontrado. Mantendo a rede já embutida."
    exit 0
}

$pastaDestino = Split-Path -Path $Destino -Parent
if (-not (Test-Path $pastaDestino)) {
    New-Item -ItemType Directory -Path $pastaDestino -Force | Out-Null
}

Copy-Item -Path $melhor.Arquivo.FullName -Destination $Destino -Force
Write-Host "EmbutirRedeTreinada: embutida '$($melhor.Arquivo.Name)' (geração $($melhor.Geracao))."
