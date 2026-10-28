using System.Windows;
using System.Windows.Controls;
using RadarTorres.App.ViewModels;
using RadarTorres.App.Views.Shared;

namespace RadarTorres.App.Views;

/// <summary>
/// Code-behind da tela "Usuários" (exclusiva do Administrador — ver
/// <see cref="Services.IPermissionService.PodeVerMenu"/>). A única lógica aqui é abrir as janelas
/// modais de cadastro/edição e de redefinição de senha: elas compartilham a mesma
/// <see cref="UsuariosViewModel"/>, então validação e gravação continuam inteiramente na
/// ViewModel, como antes de os formulários saírem da barra lateral.
/// </summary>
public partial class UsuariosView : UserControl
{
    private readonly UsuariosViewModel _viewModel;

    public UsuariosView(UsuariosViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;

        // View é Singleton (ver App.xaml.cs) — Loaded refaz a cada navegação pela barra
        // lateral, mesmo padrão de ObjetosDetectadosView.
        Loaded += (_, _) => _viewModel.Reload();
    }

    /// <summary>
    /// "Novo usuário": limpa o formulário via <c>NovoCommand</c> ANTES de abrir o modal, senão a
    /// janela apareceria preenchida com os dados da linha selecionada na tabela.
    /// </summary>
    private void BotaoNovo_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.NovoCommand.CanExecute(null)) return;
        _viewModel.NovoCommand.Execute(null);

        AbrirEdicao();
    }

    /// <summary>
    /// "Editar": o formulário já está carregado com o usuário selecionado — a ViewModel preenche
    /// os campos no setter de <c>SelectedUsuario</c> —, então basta abrir o modal.
    /// </summary>
    private void BotaoEditar_Click(object sender, RoutedEventArgs e) => AbrirEdicao();

    /// <summary>Duplo clique na linha é atalho para editar, comportamento esperado numa tabela administrativa.</summary>
    private void Tabela_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_viewModel.SelectedUsuario is null) return;
        AbrirEdicao();
    }

    private void BotaoRedefinirSenha_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedUsuario is null) return;

        _viewModel.LimparRedefinicaoSenha();
        JanelaModal.Abrir(new RedefinirSenhaWindow(_viewModel), this);

        // A redefinição não altera a listagem, mas o campo "Último acesso" e o estado da conta
        // podem ter mudado em outra sessão — recarregar mantém a tabela coerente com o banco.
        _viewModel.Reload();
    }

    private void AbrirEdicao()
    {
        if (!_viewModel.PodeGerenciar) return;

        JanelaModal.Abrir(new UsuarioEdicaoWindow(_viewModel), this);

        // Após salvar, a tabela precisa refletir o registro novo/alterado.
        _viewModel.Reload();
    }
}
