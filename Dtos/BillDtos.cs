namespace Buffet_Restaurant_API.Dtos
{
    public class CreateWalkInBillDto
    {
        public int Config_id { get; set; }
        public List<int> Table_ids { get; set; } = new();
        public int Emp_id { get; set; }
        public int NumAdults { get; set; }
        public int NumChildren { get; set; }
        public int? Discount_id { get; set; } // เพิ่มเข้ามาตอนเปิดบิล Walk-in
    }

    public class CreateBookingBillDto
    {
        public int Config_id { get; set; }
        public int Emp_id { get; set; }
        public int Discount_id { get; set; } // เพิ่มเข้ามาตอนยืนยันลูกค้าจองหน้าร้าน
    }

    public class CloseBillDto
    {
        public decimal Fine_kg { get; set; }
        public decimal Total_amount { get; set; }
        public string PaymentMethod { get; set; } = null!;
        public int? Discount_id { get; set; } // เพิ่มเข้ามาตอนปิดบิล
    }
    public class UpdateBillDto
    {
        public int Bill_id { get; set; }
        public decimal Fine_kg { get; set; }
        public int NumAdults { get; set; }
        public int NumChildren { get; set; }
        public int? Discount_id { get; set; }
    }

    public class UpdatePaymentMethodDto
    {
        public string PaymentMethod { get; set; } = string.Empty; 
        public string? TransactionId { get; set; } 
    }
}