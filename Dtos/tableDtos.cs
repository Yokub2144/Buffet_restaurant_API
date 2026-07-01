namespace Buffet_Restaurant_Managment_System_API.Dtos
{
    public class tableDtos
    {
        public string Table_Number { get; set; } = null!;
    }
    public class ChangeTableDto
    {
        public List<int> Table_ids { get; set; } = new List<int>();
    }
}