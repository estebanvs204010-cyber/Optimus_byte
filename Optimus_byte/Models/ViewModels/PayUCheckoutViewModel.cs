public class PayUCheckoutViewModel
{
    public int IdCompra { get; set; } // ← Añadido para corregir el error
    public int IdFactura { get; set; }
    public decimal Total { get; set; }
    public decimal Iva { get; set; }
    public decimal Base { get; set; }
    public string MerchantId { get; set; }
    public string AccountId { get; set; }
    public string Description { get; set; }
    public string ReferenceCode { get; set; }
    public string Amount { get; set; }
    public string Tax { get; set; }
    public string TaxReturnBase { get; set; }
    public string Currency { get; set; }
    public string Signature { get; set; }
    public string Test { get; set; }
    public string BuyerEmail { get; set; }
    public string ResponseUrl { get; set; }
    public string ConfirmUrl { get; set; }
    public string CheckoutUrl { get; set; }
}