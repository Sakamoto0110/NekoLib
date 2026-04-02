namespace NekoLib.Navigation.Metadata {
    public readonly struct ModalResult
    {
        public bool Confirmed { get; }
        public object Value { get; }

        public ModalResult(bool confirmed, object value = null)
        {
            Confirmed = confirmed;
            Value = value;
        }

        public static ModalResult Ok(object value = null)
            => new ModalResult(true, value);

        public static ModalResult Cancel()
            => new ModalResult(false, null);
    }
}
