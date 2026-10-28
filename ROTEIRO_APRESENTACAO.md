# Roteiro de apresentação — RadarTorres

> Ver também: [Índice](Índice.md) (raiz do vault).

## O que é

Aplicativo desktop (C#/WPF) que se comunica via porta serial com um Arduino para detectar alvos ao redor de uma base, localizá-los em um radar gráfico, determinar automaticamente o quadrante de cada alvo, selecionar a torre demonstrativa mais adequada e acionar um indicador demonstrativo (laser de baixa potência / LED / simulação — nunca armamento real). Desenvolvido como Trabalho de Conclusão de Curso (TCC) em Engenharia da Computação, em dupla.

## Vantagens / diferenciais a destacar

- **Projeto full-stack hardware + software**: integra firmware Arduino (protocolo serial próprio, documentado) com uma aplicação desktop MVVM em C#/WPF — bom para mostrar visão de sistema ponta a ponta, não só código de aplicativo.
- **Funciona sem hardware nenhum**: modo de simulação embutido gera alvos fictícios e permite demonstrar o sistema completo (localização, seleção de torre, acionamento) sem depender de um Arduino físico presente — essencial para entrevistas remotas ou sem o kit em mãos.
- **Algoritmo de seleção documentado e explicável**: conversão de coordenadas polares para cartesianas, determinação de quadrante e seleção por distância Euclidiana entre torres disponíveis — dá para explicar a matemática em poucos minutos.
- **Arquitetura limpa**: separação estrita Views (XAML, sem lógica) / ViewModels / Services (100% das regras, nenhum referencia WPF) / Models — fácil de testar e de justificar decisões arquiteturais em entrevista.
- **Distribuição profissional**: instalador Windows self-contained via Inno Setup, preserva configurações do usuário em updates, com launcher avulso separado — mostra preocupação com experiência do usuário final, não só com o código.
- **Configuração sem recompilar**: posição e quantidade de torres vêm de `appsettings.json`, não hardcoded — fácil de demonstrar flexibilidade ao vivo.
- **Base acadêmica robusta**: requisitos, casos de uso, diagrama de classes, modelo de dados e artigo do TCC documentados — útil se o avaliador quiser profundidade acadêmica, não só técnica.

## Tecnologias

C# 13 / .NET 9 · WPF (MVVM) · `System.IO.Ports` (comunicação serial) · `Microsoft.Extensions.DependencyInjection` + `Microsoft.Extensions.Configuration` · Persistência local em CSV · Inno Setup 6 (instalador) · Firmware Arduino (C/C++).

## Como apresentar (roteiro sugerido)

1. Abra o app já logado (ou mostre o login rapidamente) e vá direto ao painel de **CONTROLE DO SISTEMA**.
2. Ative o **modo de simulação** — não depende de Arduino físico, então é a forma mais segura e rápida de demonstrar em qualquer lugar.
3. Selecione o modo "Localização + seleção automática de torre" e explique o radar em tempo real: quadrantes, torres, alvos se movendo.
4. Mude para o modo "Localização + acionamento demonstrativo automático" e mostre o acionamento acontecendo quando um alvo se aproxima de uma torre — deixe claro que é **só indicador demonstrativo**, nunca armamento real.
5. Se o avaliador for técnico, abra `appsettings.json` e mostre como adicionar/mover uma torre sem recompilar.
6. Se houver Arduino físico disponível e tempo, conecte via USB e repita a demo com dados reais — use isso como "bônus", não como dependência da apresentação.
7. Encerre citando a arquitetura MVVM e, se for contexto acadêmico, ofereça os documentos do TCC (requisitos, UML, artigo) como aprofundamento.

**Avisos de segurança para a fala**: sempre deixar claro que o acionamento é demonstrativo (laser de baixa potência/LED/simulação) e respeita distância mínima de segurança — nunca armamento real. Evitar termos que soem como aplicação militar ofensiva; o enquadramento é de **projeto acadêmico de detecção e automação**.
