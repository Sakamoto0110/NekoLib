using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Contracts.Runtime;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Metadata.Attributes;
using NekoLib.Navigation.WinForms.Hosting;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NekoLib.Navigation.WinForms.Pages
{
    [PageMetadata(Name = "ConfirmDialog", Presentation = PagePresentationMode.ModalOverlay)]
    public partial class ConfirmDialog : PopupView, IPageOverlay<string>
    {
        // The property the OverlayService will read when resolving the TCS
        public string Result { get; private set; }

        public bool HasResult => !(string.IsNullOrEmpty(Result) || string.IsNullOrWhiteSpace(Result));

        public ConfirmDialog()
        {
            InitializeComponent();

            // Setup your UI (e.g., buttons, labels)
            // btnYes.Click += BtnYes_Click;
        }

         

        public void SetResult(string result)
        {
            Result = result;
        }

        private void btnAccept_Click(object sender, EventArgs e)
        {
            SetResult("Confirmed");


            ClosePopup(); // Hides UI, resolves TCS, unblocks the await
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            SetResult("Cancelled");

            ClosePopup();

        }
    }
}


