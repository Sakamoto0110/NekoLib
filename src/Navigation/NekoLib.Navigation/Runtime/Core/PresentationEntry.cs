using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Metadata;
using System.Threading.Tasks;

namespace NekoLib.Navigation.Runtime.Core
{
    internal sealed class PresentationEntry
    {
        public IPageView View { get; }
        public PageDescriptor Descriptor { get; }

        private readonly TaskCompletionSource<ModalResult> _modalTcs;

        public bool IsOverlay => Descriptor.Presentation != PagePresentationMode.Replace;
        public bool IsModalOverlay => Descriptor.Presentation == PagePresentationMode.ModalOverlay;

        private PresentationEntry(
            IPageView view,
            PageDescriptor descriptor,
            TaskCompletionSource<ModalResult> modalTcs)
        {
            View = view;
            Descriptor = descriptor;
            _modalTcs = modalTcs;
        }

        public static PresentationEntry ForBase(IPageView view, PageDescriptor desc)
            => new PresentationEntry(view, desc, null);

        public static PresentationEntry ForOverlay(IPageView view, PageDescriptor desc)
            => new PresentationEntry(view, desc, null);

        public static PresentationEntry ForModal(
            IPageView view,
            PageDescriptor desc,
            TaskCompletionSource<ModalResult> tcs)
            => new PresentationEntry(view, desc, tcs);

        public void CompleteModal(ModalResult result)
        {
            _modalTcs?.TrySetResult(result);
        }
    }
}

 