using System;
using System.ComponentModel;
using System.Windows;

namespace RadarTorres.App.Views.Shared;

/// <summary>
/// Abertura padronizada das janelas modais de formulário do projeto (diretriz geral de UX: o
/// conteúdo principal ocupa a tela inteira e as ações ficam no canto superior direito, abrindo um
/// modal centralizado com apenas os campos daquela operação).
///
/// Existe para que cada tela não repita o par <c>Owner = Window.GetWindow(this)</c> +
/// <c>ShowDialog()</c> — esquecer o <c>Owner</c> faz o modal abrir no centro do monitor em vez de
/// centralizado sobre a janela do aplicativo, e o padrão só fica consistente se todas as telas o
/// definirem. Ver <c>Styles/Modal.xaml</c> para os estilos visuais correspondentes.
/// </summary>
public static class JanelaModal
{
    /// <summary>
    /// Mostra <paramref name="janela"/> como modal centralizada sobre a janela que contém
    /// <paramref name="origem"/>. Devolve o <see cref="Window.DialogResult"/>.
    /// </summary>
    public static bool? Abrir(Window janela, DependencyObject origem)
    {
        ArgumentNullException.ThrowIfNull(janela);

        janela.Owner = origem is null ? null : Window.GetWindow(origem);
        janela.WindowStartupLocation = janela.Owner is null
            ? WindowStartupLocation.CenterScreen
            : WindowStartupLocation.CenterOwner;

        return janela.ShowDialog();
    }

    /// <summary>
    /// Fecha <paramref name="janela"/> automaticamente quando a propriedade booleana
    /// <paramref name="propriedadeDeSucesso"/> do DataContext passar a <c>true</c> — isto é,
    /// quando a operação do formulário se concluir com êxito.
    ///
    /// Com isso o modal não precisa de um comando próprio de fechamento nem de lógica duplicada:
    /// ele reaproveita o MESMO comando que a tela já usava (Salvar, Redefinir...), que continua
    /// responsável por validar e gravar. Em caso de erro a propriedade permanece <c>false</c>, a
    /// janela fica aberta e a mensagem de validação aparece no próprio modal.
    ///
    /// A assinatura é desfeita no <see cref="Window.Closed"/> para não segurar referência à
    /// ViewModel (que é singleton no contêiner de DI) depois que o modal morre.
    /// </summary>
    public static void FecharQuando(Window janela, string propriedadeDeSucesso)
    {
        ArgumentNullException.ThrowIfNull(janela);

        if (janela.DataContext is not INotifyPropertyChanged observavel) return;

        void AoMudar(object? _, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != propriedadeDeSucesso) return;
            if (!LerBooleano(janela.DataContext, propriedadeDeSucesso)) return;

            janela.DialogResult = true;
            janela.Close();
        }

        observavel.PropertyChanged += AoMudar;
        janela.Closed += (_, _) => observavel.PropertyChanged -= AoMudar;
    }

    private static bool LerBooleano(object? alvo, string propriedade) =>
        alvo?.GetType().GetProperty(propriedade)?.GetValue(alvo) is true;
}
