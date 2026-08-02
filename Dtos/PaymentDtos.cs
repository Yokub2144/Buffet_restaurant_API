namespace Buffet_Restaurant_API.Dtos
{
public class CheckoutQrRequestDto
{
    public int BillId { get; set; }
    public decimal TotalAmount { get; set; } 
}

public class VerifyPaymentRequestDto
{
    public int BillId { get; set; }
    public string TransactionId { get; set; }
}
}