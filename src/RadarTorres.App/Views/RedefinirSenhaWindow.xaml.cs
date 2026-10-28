using System.Windows;
using RadarTorres.App.ViewModels;
using RadarTorres.App.Views.Shared;

namespace RadarTorres.App.Views;

/// <summary>
/// Code-behind do modal de redefinição de senha. Compartilha a <see cref="UsuariosViewModel"/> da
/// tela; a validação e a gravação continuam em <c>RedefinirSenhaCommand</c>. Fecha sozinha quando
/// a operação dá certo e permanece aberta, com a mensagem de erro visível, quando não dá.
/// </summary>
public partial class RedefinirSenhaWindow : Window
{
    public RedefinirSenhaWindow(UsuariosViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;
        JanelaModal.FecharQuando(this, nameof(UsuariosViewModel.SucessoRedefinir));
    }
}
