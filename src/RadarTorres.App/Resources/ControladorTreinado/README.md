# Genes treinados — branch `genes-treinamento`

Esta branch existe para guardar os genes (redes e genomas) treinados durante a construção do
RadarTorres. Os arquivos versionados aqui são:

- `rede-controlador.net` — rede treinada embutida no executável pelo `.csproj` quando o arquivo existe;
- `genoma-controlador.json` — genoma correspondente, em formato legível.

## Por que a branch existe

1. **Rastrear estagnação de gene.** Manter os genes de cada rodada permite acompanhar em que ponto
   a evolução parou de progredir e comparar gerações entre si.
2. **Evitar retreino completo a cada ajuste.** Com os genes guardados, um ajuste pontual no
   controlador não obriga a treinar do zero.

## Esta branch não é o estado entregue

A versão entregue do sistema **não inclui rede treinada**. Na `main`, os dois arquivos acima foram
removidos do versionamento (`git rm --cached`) e o diretório
`src/RadarTorres.App/Resources/ControladorTreinado/` está no `.gitignore`, de modo que um executável
compilado a partir da `main` usa a heurística pura. Os arquivos continuam existindo no disco das
máquinas de desenvolvimento; apenas deixaram de ser versionados.

Portanto, esta branch serve como material de apoio ao desenvolvimento e **não deve ser usada como
referência do comportamento entregue**.

## Previsão de exclusão

A previsão é excluir esta branch ao fim do projeto.

> **Aviso:** excluir a branch apaga estes genes. Se o histórico de estagnação precisar ser
> preservado, a branch deve ser mantida, ou os arquivos `.net` devem ser guardados como anexo de
> release antes da exclusão.
