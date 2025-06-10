namespace EDIOrderData
{
    public class EDIOrder
    {
        public string MessageReference { get; set; } = string.Empty;
        public string DocumentNumber { get; set; } = string.Empty;
        public string DocumentDate { get; set; } = string.Empty;
        public string DeliveryDate { get; set; } = string.Empty;
        public string BuyerId { get; set; } = string.Empty;
        public string BuyerName { get; set; } = string.Empty;
        public string SupplierId { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string ItemNumber { get; set; } = string.Empty;
        public string Quantity { get; set; } = "0";
        public string UnitPrice { get; set; } = "0";
        public string LineAmount { get; set; } = "0";
    }
}