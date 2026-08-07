using System;
using System.Windows.Controls;

namespace RadarTorres.App.Services;

/// <summary>
/// Views que precisam saber quando voltam a ficar visíveis (ex.: para recarregar dados do
/// painel principal) implementam esta interface; <see cref="INavigationService"/> chama
/// <see cref="OnNavigatedTo"/> automaticamente após a navegação.
/// </summary>
public interface INavigationAware
{
    void OnNavigatedTo();
}

/// <summary>
/// Navegação simples entre as telas hospedadas pela <c>ShellWindow</c> (Requisito 2 — barra
/// lateral). Não é um framework de navegação completo (sem histórico/voltar) — apenas troca o
/// conteúdo do <c>ContentControl</c> hospedeiro pela view correspondente ao item selecionado.
/// </summary>
public interface INavigationService
{
    /// <summary>Item atualmente exibido no conteúdo principal.</summary>
    MenuItem? CurrentItem { get; }

    event EventHandler<MenuItem>? Navigated;

    /// <summary>Define o <see cref="ContentControl"/> que hospeda a view atual (chamado uma vez pela ShellWindow).</summary>
    void SetHost(ContentControl host);

    void NavigateTo(MenuItem item);
}
