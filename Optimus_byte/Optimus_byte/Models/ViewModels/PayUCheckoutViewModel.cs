namespace Optimus_byte.Models.ViewModels
{
    public class PayUCheckoutViewModel // ← Cambie el nombre aquí si necesita ambas clases
    {
        public int IdCompra { get; set; }
        public int IdFactura { get; set; }
        public decimal Total { get; set; }
        public decimal Iva { get; set; }
        public decimal Base { get; set; }
        public string MerchantId { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ReferenceCode { get; set; } = string.Empty;
        public string Amount { get; set; } = string.Empty;
        public string Tax { get; set; } = string.Empty;
        public string TaxReturnBase { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
        public string Test { get; set; } = string.Empty;
        public string BuyerEmail { get; set; } = string.Empty;
        public string ResponseUrl { get; set; } = string.Empty;
        public string ConfirmUrl { get; set; } = string.Empty;
        public string CheckoutUrl { get; set; } = string.Empty;
    }
}