using System;
using System.Windows.Input;
using NekoLib.Mvvm;

namespace NekoLib.Navigation.RuntimeTests.Winforms481.Pages.TextInputPrompt
{
    public sealed class TextInputPromptViewModel : ViewModelBase
    {
        private string _inputText = string.Empty;

        public string InputText
        {
            get { return _inputText; }
            set { SetProperty(ref _inputText, value); }
        }

        public ICommand OkCommand { get; }

        public TextInputPromptViewModel(Action<string> onComplete)
        {
            if (onComplete == null) throw new ArgumentNullException("onComplete");

            OkCommand = new RelayCommand(_ => onComplete(InputText));
        }
    }
}
