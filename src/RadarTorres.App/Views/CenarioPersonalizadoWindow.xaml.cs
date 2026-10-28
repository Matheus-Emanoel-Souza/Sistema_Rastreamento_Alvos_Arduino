using System;
using System.Windows;
using RadarTorres.App.ViewModels;

namespace RadarTorres.App.Views;

/// <summary>
/// Janela modal de cadastro do cenário personalizado (ver comentário no XAML). Não tem
/// ViewModel própria — reaproveita a mesma <see cref="TreinamentoControladorViewModel"/> da tela
/// que a abriu, passada como <see cref="FrameworkElement.DataContext"/> pelo chamador.
/// </summary>
public partial class CenarioPersonalizadoWindow : Window
{
    public CenarioPersonalizadoWindow(TreinamentoControladorViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;

        viewModel.CenarioPersonalizadoIniciado += OnCenarioPersonalizadoIniciado;
        Closed += (_, _) => viewModel.CenarioPersonalizadoIniciado -= OnCenarioPersonalizadoIniciado;
    }

    private void OnCenarioPersonalizadoIniciado(object? sender, EventArgs e) => Close();

    private void BotaoCancelar_Click(object sender, RoutedEventArgs e) => Close();
}
