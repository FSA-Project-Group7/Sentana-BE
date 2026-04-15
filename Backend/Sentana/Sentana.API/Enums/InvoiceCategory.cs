namespace Sentana.API.Enums
{
    public enum InvoiceCategory
    {
        /// <summary>
        /// Hóa đơn trả thêm (khi thanh lý hợp đồng, cư dân còn nợ)
        /// </summary>
        AdditionalPayment = 1,

        /// <summary>
        /// Hóa đơn tiền tháng (tiền thuê, điện, nước, dịch vụ)
        /// </summary>
        MonthlyPayment = 2
    }
}
